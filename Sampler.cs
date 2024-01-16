using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SampleName
{
	public class Sampler
	{
		public static List<int> IntensityLevels = new List<int>
		{
				0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30
		};

		public const int Level = 4;
		public const double ConfidenceThreshold = 0.5;

		private int aLimit;
		private Dictionary<string, Dictionary<string, object>> pointsDict;
		private Dictionary<string, Dictionary<string, object>> pointsPool;
		private Dictionary<string, Dictionary<string, object>> sampledPool;
		private int batchNum;
		private int defaultIntensity;
		private int countSamples;

		public Sampler(int aLimit = 24, string filename = "./points/points_24.json")
		{
			this.aLimit = aLimit;
			this.pointsDict = LoadPoints(filename);
			this.pointsPool = BuildPool();
			this.sampledPool = new Dictionary<string, Dictionary<string, object>>();
			this.batchNum = 1;
			this.defaultIntensity = IntensityLevels[IntensityLevels.Count / 2];
			this.countSamples = 0;
		}

		private Dictionary<string, Dictionary<string, object>> BuildPool()
		{
			var pool = new Dictionary<string, Dictionary<string, object>>();
			foreach (var pntId in this.pointsDict["tier_1"].Keys)
			{
				pool[pntId] = new Dictionary<string, object>
						{
								{ "point", this.pointsDict["tier_1"][pntId]["point"] },
								{ "priority", 1 },
								{ "history", new List<Dictionary<string, object>>() },
								{ "final_intensity", null }
						};
			}

			var midPoint = (this.aLimit / 2.0, this.aLimit / 2.0);
			var tmpId = pool.Keys.OrderBy(x => GetDistance((Tuple<double, double>)pool[x]["point"], midPoint)).First();
			var tmpPnt = GetPnt(tmpId, this.aLimit);
			var tmpPnt1 = (-tmpPnt.Item1, tmpPnt.Item2);
			var tmpPnt2 = (-tmpPnt.Item1, -tmpPnt.Item2);
			var tmpPnt3 = (tmpPnt.Item1, -tmpPnt.Item2);
			var tmpId1 = GetId(tmpPnt1, this.aLimit);
			var tmpId2 = GetId(tmpPnt2, this.aLimit);
			var tmpId3 = GetId(tmpPnt3, this.aLimit);

			foreach (var pntId in new List<string> { tmpId, tmpId1, tmpId2, tmpId3 })
			{
				pool[pntId]["priority"] = 3;
			}

			return pool;
		}

		public List<Dictionary<string, object>> SampleBatch(int batchSize = 8)
		{
			List<string> bpIds;
			if (this.batchNum == 1)
			{
				bpIds = GetNextBatch(batchSize / 2);
			}
			else
			{
				bpIds = GetNextBatch(batchSize);
			}

			var batch = new List<Dictionary<string, object>>();
			foreach (var pntId in OrderBatch(bpIds))
			{
				var (intensity, step) = GetIntensity(pntId);
				batch.Add(new Dictionary<string, object>
						{
								{ "id", pntId },
								{ "point", this.pointsPool[pntId]["point"] },
								{ "intensity", intensity },
								{ "step", step }
						});
			}

			this.batchNum += 1;
			this.countSamples += batch.Count;
			return batch;
		}

		public void CollectResponse(List<Dictionary<string, object>> responses)
		{
			foreach (var resp in responses)
			{
				var id = (string)resp["id"];
				var history = new Dictionary<string, object>
						{
								{ "intensity", resp["intensity"] },
								{ "step", resp["step"] },
								{ "conf", resp["conf"] },
								{ "see", resp["see"] }
						};
				((List<Dictionary<string, object>>)this.pointsPool[id]["history"]).Add(history);

				if ((double)resp["conf"] >= ConfidenceThreshold)
				{
					if ((string)resp["step"] == "half")
					{
						if ((bool)resp["see"])
						{
							this.pointsPool[id]["final_intensity"] = resp["intensity"];
						}
						else
						{
							var idx = FindNextIndex(id, -2);
							if ((bool)((List<Dictionary<string, object>>)this.pointsPool[id]["history"])[idx]["see"])
							{
								this.pointsPool[id]["final_intensity"] = ((List<Dictionary<string, object>>)this.pointsPool[id]["history"])[idx]["intensity"];
							}
							else
							{
								idx = FindNextIndex(id, idx - 1);
								this.pointsPool[id]["final_intensity"] = ((List<Dictionary<string, object>>)this.pointsPool[id]["history"])[idx]["intensity"];
							}

							this.pointsPool[id]["priority"] = 0;
						}
					}
					else
					{
						this.pointsPool[id]["priority"] += 1;

						if ((int)resp["intensity"] >= IntensityLevels[IntensityLevels.Count - 2])
						{
							if (!((bool)resp["see"]))
							{
								this.pointsPool[id]["final_intensity"] = IntensityLevels[IntensityLevels.Count - 1];
								this.pointsPool[id]["priority"] = 0;
							}
						}
						else if ((int)resp["intensity"] <= IntensityLevels[1])
						{
							if ((bool)resp["see"])
							{
								this.pointsPool[id]["final_intensity"] = IntensityLevels[1];
								this.pointsPool[id]["priority"] = 0;
							}
						}
					}
				}
			}
		}

		private int FindNextIndex(string id, int idx)
		{
			while ((double)((List<Dictionary<string, object>>)this.pointsPool[id]["history"])[idx]["conf"] < ConfidenceThreshold)
			{
				idx -= 1;
			}

			return idx;
		}

		private Tuple<int, string> GetIntensity(string pntId)
		{
			var hstry = (List<Dictionary<string, object>>)this.pointsPool[pntId]["history"];
			if (hstry.Count > 0)
			{
				var intensity = (int)hstry.Last()["intensity"];
				var step = (string)hstry.Last()["step"];

				if (hstry.Count > 0)
				{
					if ((double)hstry.Last()["conf"] >= ConfidenceThreshold)
					{
						if ((string)hstry.Last()["step"] == "full")
						{
							if ((bool)hstry.Last()["see"])
							{
								if (intensity > 0)
								{
									var (level, s) = GetStepSize(hstry);
									return Tuple.Create(intensity - level, s);
								}
								else
								{
									Console.WriteLine("Intensity went below zero");
									return null;
								}
							}
							else
							{
								if (intensity < 30)
								{
									var (level, s) = GetStepSize(hstry);
									return Tuple.Create(intensity + level, s);
								}
								else
								{
									Console.WriteLine("Intensity went above 30");
									return null;
								}
							}
						}
						else
						{
							return null;
						}
					}
					else
					{
						return Tuple.Create(intensity, step);
					}
				}
				else
				{
					var intnst = GetIntensityFromNeighbors(pntId);
					return Tuple.Create(intnst, "full");
				}
			}
			else
			{
				return Tuple.Create(GetIntensityFromNeighbors(pntId), "full");
			}
		}

		private Tuple<int, string> GetStepSize(List<Dictionary<string, object>> history)
		{
			var flags = history.Where(h => (double)h["conf"] >= ConfidenceThreshold).Select(h => (int)h["see"]).ToList();
			if (flags.Count <= 1)
			{
				return Tuple.Create(Level, "full");
			}

			if (flags.Sum() == 0 || flags.Sum() == flags.Count)
			{
				return Tuple.Create(Level, "full");
			}
			else
			{
				return Tuple.Create(Level / 2, "half");
			}
		}

		private int GetIntensityFromNeighbors(string pntId)
		{
			var nghbrs = (List<string>)this.pointsDict["tier_1"][pntId]["n_tier_1"];
			var intensities = new List<int>();
			var sampled = this.pointsPool.Keys.Where(_id => (int)this.pointsPool[_id]["priority"] == 0).ToList();
			foreach (var _id in nghbrs)
			{
				if (sampled.Contains(_id))
				{
					if (this.pointsPool[_id]["final_intensity"] != null)
					{
						intensities.Add((int)this.pointsPool[_id]["final_intensity"]);
					}
				}
			}

			if (intensities.Count > 0)
			{
				var ind = Array.BinarySearch(IntensityLevels.ToArray(), (int)Math.Round(intensities.Average()));
				ind = (ind < 0) ? ~ind - 1 : ind;
				return IntensityLevels[ind];
			}
			else
			{
				return IntensityLevels[IntensityLevels.Count / 2];
			}
		}

		private List<string> GetNextBatch(int numSamples)
		{
			var batch = this.pointsPool.Keys
					.OrderBy(x => (int)this.pointsPool[x]["priority"])
					.Take(numSamples)
					.Where(pntId => (int)this.pointsPool[pntId]["priority"] > 0)
					.ToList();
			return batch;
		}

		private Tuple<double, double> GetPnt(string id, int aLimit)
		{
			var y = Math.Round(float.Parse(id), -4) / 1000000 - aLimit;
			var x = (float.Parse(id) - (y + aLimit) * 1000000) / 100;
			return Tuple.Create(x, y);
		}

		private string GetId(Tuple<double, double> point, int aLimit)
		{
			return $"{(int)((aLimit + Math.Round(point.Item2, 2)) * 10000000 + 100 * Math.Round(point.Item1, 2))}";
		}

		private List<string> OrderBatch(List<string> pnts)
		{
			return pnts;
		}

		private void AdaptPriorities()
		{
			// To be implemented
		}

		private static Dictionary<string, Dictionary<string, object>> LoadPoints(string filename)
		{
			using (var reader = new StreamReader(filename))
			{
				var pointsDct = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(reader.ReadToEnd());
				return pointsDct;
			}
		}

		private double GetDistance(Tuple<double, double> p1, Tuple<double, double> p2)
		{
			return Math.Sqrt(Math.Pow(p1.Item1 - p2.Item1, 2) + Math.Pow(p1.Item2 - p2.Item2, 2));
		}
	}

	class Program
	{
		static void Main()
		{
			var s = new Sampler();
			Console.WriteLine(JsonConvert.SerializeObject(s.pointsDict["tier_1"], Formatting.Indented));
			foreach (var (k, v) in s.pointsDict["tier_1"])
			{
				Console.WriteLine($"{k}: {JsonConvert.SerializeObject(v)}");
			}

			foreach (var (k, v) in s.pointsPool)
			{
				Console.WriteLine($"{k}: {JsonConvert.SerializeObject(v)}");
				var p = (Tuple<double, double>)v["point"];
				Console.WriteLine($"{k}: {Math.Round(float.Parse(k), -4)}");
				var y = Math.Round(float.Parse(k), -4) / 1000000 - 24;
				var x = (float.Parse(k) - (y + 24) * 1000000) / 100;
				Console.WriteLine($"{x}, {y}");
			}
		}
	}
}
