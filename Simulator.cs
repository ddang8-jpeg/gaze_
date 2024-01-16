using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms;
using Newtonsoft.Json;
using Sampler = SampleName.Sampler;


public class Simulator
{
	private int a_limit;
	private string filename;
	private Dictionary<string, Dictionary<string, Dictionary<string, object>>> pnts_dct;
	private Sampler sampler;
	private Dictionary<string, Dictionary<string, object>> visual_field;

	public Simulator(int a_limit = 24, string filename = "./points/points_24.json")
	{
		this.a_limit = a_limit;
		this.filename = filename;

		this.pnts_dct = LoadPoints(filename);
		this.sampler = new Sampler(this.a_limit, filename);
		this.visual_field = BuildVisualField();
	}

	public void Run(bool visualize = true, int batch_size = 8)
	{
		int num_pts = sampler.PntsPool.Count;
		while (num_pts > 0)
		{
			var batch = sampler.SampleBatch(batch_size);
			var responses = SimulateResponse(batch);
			sampler.CollectResponse(responses);
			num_pts = sampler.PntsPool.Count(id => sampler.PntsPool[id]["priority"].ToString() == "0");

			if (visualize)
			{
				var chart = DisplaySimStep(responses);
				chart.WaitOnLoad();
				chart.Dispose();
			}
		}
	}

	private Chart DisplaySimStep(List<Dictionary<string, object>> responses)
	{
		var chart = DisplayVisFld(visual_field, "winter");
		foreach (var id in sampler.PntsPool.Keys)
		{
			if (sampler.PntsPool[id]["priority"].ToString() == "0")
			{
				double x = Convert.ToDouble(sampler.PntsPool[id]["point"][0]);
				double y = Convert.ToDouble(sampler.PntsPool[id]["point"][1]);
				double intnst = Convert.ToDouble(sampler.PntsPool[id]["final_intensity"]);
				chart.Series[0].Points.Add(new DataPoint(x, y, intnst));
			}
		}

		foreach (var response in responses)
		{
			if (Convert.ToDouble(response["conf"]) >= sampler.CONF_TH)
			{
				string color = (bool)response["see"] ? "tab:orange" : "tab:grey";
				double x = Convert.ToDouble(response["point"][0]);
				double y = Convert.ToDouble(response["point"][1]);
				double intensity = Convert.ToDouble(response["intensity"]);
				chart.Series[0].Points.Add(new DataPoint(x, y, intensity) { Color = System.Drawing.Color.FromName(color) });
			}
			else
			{
				double x = Convert.ToDouble(response["point"][0]);
				double y = Convert.ToDouble(response["point"][1]);
				double intensity = Convert.ToDouble(response["intensity"]);
				chart.Series[0].Points.Add(new DataPoint(x, y, intensity) { Color = System.Drawing.Color.Black });
			}
		}

		return chart;
	}

	private List<Dictionary<string, object>> SimulateResponse(List<Dictionary<string, object>> batch)
	{
		var responses = new List<Dictionary<string, object>>();
		foreach (var pnt in batch)
		{
			bool see = Convert.ToDouble(pnt["intensity"]) >= Convert.ToDouble(visual_field[pnt["id"].ToString()]["intensity"]);
			var response = new Dictionary<string, object>
								{
										{ "id", pnt["id"] },
										{ "point", pnt["point"] },
										{ "intensity", pnt["intensity"] },
										{ "step", pnt["step"] },
										{ "conf", Math.Min(5 * new Random().NextDouble(), 1) },
										{ "see", see }
								};
			responses.Add(response);
		}
		return responses;
	}

	private Dictionary<string, Dictionary<string, object>> BuildVisualField()
	{
		var visualField = new Dictionary<string, Dictionary<string, object>>();
		var pnts = pnts_dct["tier_1"].Keys.ToList();
		int numDef = new Random().Next(0, 3);

		if (numDef > 0)
		{
			var darkPoints = pnts.OrderBy(x => new Random().Next()).Take(numDef);
			foreach (var pnt in darkPoints)
			{
				int idx = new Random().Next(6);
				visualField[pnt] = new Dictionary<string, object>
										{
												{ "point", pnts_dct["tier_1"][pnt]["point"] },
												{ "intensity", Math.Max((double)sampler.INTENSITY_LEVELS[idx] + new Random().NextDouble(), 0) }
										};
			}
		}
		else
		{
			var pnt = pnts.OrderBy(x => new Random().Next()).First();
			visualField[pnt] = new Dictionary<string, object>
								{
										{ "point", pnts_dct["tier_1"][pnt]["point"] },
										{ "intensity", Math.Max(16 + new Random().NextDouble(), 0) }
								};
		}

		var frontier = new List<string>(visualField.Keys);
		while (frontier.Count > 0)
		{
			var nextFrontier = new List<string>();
			foreach (var pnt in frontier)
			{
				foreach (var nPnt in pnts_dct["tier_1"][pnt]["n_tier_1"])
				{
					if (!visualField.ContainsKey(nPnt))
					{
						double intnsty = (double)visualField[pnt]["intensity"] +
														 new Random().Next(new int[] { 2, 3, 4 }) + new Random().NextDouble();
						visualField[nPnt] = new Dictionary<string, object>
														{
																{ "point", pnts_dct["tier_1"][nPnt]["point"] },
																{ "intensity", Math.Min(Math.Max(intnsty, 0), 30) }
														};
						nextFrontier.Add(nPnt);
					}
				}
			}
			frontier = nextFrontier;
		}

		return visualField;
	}

	private Chart DisplayVisFld(Dictionary<string, Dictionary<string, object>> visualField, string cmap = "winter", List<double> color = null)
	{
		var chart = new Chart();
		chart.ChartAreas.Add(new ChartArea());
		chart.Series.Add(new Series());

		chart.ChartAreas[0].AxisX.Title = "Horizontal";
		chart.ChartAreas[0].AxisY.Title = "Vertical";
		chart.ChartAreas[0].AxisZ.Title = "Intensity";

		chart.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
		chart.ChartAreas[0].AxisY.MajorGrid.Enabled = false;
		chart.ChartAreas[0].AxisZ.MajorGrid.Enabled = false;

		var xPoints = visualField.Keys.Select(pnt => Convert.ToDouble(visualField[pnt]["point"][0])).ToArray();
		var yPoints = visualField.Keys.Select(pnt => Convert.ToDouble(visualField[pnt]["point"][1])).ToArray();
		var zPoints = visualField.Keys.Select(pnt => Convert.ToDouble(visualField[pnt]["intensity"])).ToArray();

		if (color == null)
			color = zPoints.ToList();

		for (int i = 0; i < visualField.Count; i++)
		{
			chart.Series[0].Points.Add(new DataPoint(xPoints[i], yPoints[i], zPoints[i]) { Color = System.Drawing.Color.FromArgb((int)color[i]) });
		}

		return chart;
	}

	private Chart DisplayDetectedFld(Dictionary<string, Dictionary<string, object>> visualField, Dictionary<string, Dictionary<string, object>> pntsPool)
	{
		var chart = new Chart();
		chart.ChartAreas.Add(new ChartArea());
		chart.Series.Add(new Series());
		chart.Series.Add(new Series());

		chart.ChartAreas[0].AxisX.Title = "Horizontal";
		chart.ChartAreas[0].AxisY.Title = "Vertical";
		chart.ChartAreas[0].AxisZ.Title = "Intensity";

		chart.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
		chart.ChartAreas[0].AxisY.MajorGrid.Enabled = false;
		chart.ChartAreas[0].AxisZ.MajorGrid.Enabled = false;

		var xPoints = visualField.Keys.Select(pnt => Convert.ToDouble(visualField[pnt]["point"][0])).ToArray();
		var yPoints = visualField.Keys.Select(pnt => Convert.ToDouble(visualField[pnt]["point"][1])).ToArray();
		var zPoints = visualField.Keys.Select(pnt => Convert.ToDouble(visualField[pnt]["intensity"])).ToArray();

		for (int i = 0; i < visualField.Count; i++)
		{
			chart.Series[0].Points.Add(new DataPoint(xPoints[i], yPoints[i], zPoints[i]) { Color = System.Drawing.Color.Blue });
		}

		var xPointsD = new List<double>();
		var yPointsD = new List<double>();
		var zPointsD = new List<double>();

		foreach (var id in pntsPool.Keys)
		{
			if (Convert.ToInt32(pntsPool[id]["priority"]) == 0)
			{
				double x = Convert.ToDouble(pntsPool[id]["point"][0]);
				double y = Convert.ToDouble(pntsPool[id]["point"][1]);
				double intnst = Convert.ToDouble(pntsPool[id]["final_intensity"]);
				xPointsD.Add(x);
				yPointsD.Add(y);
				zPointsD.Add(intnst);
			}
		}

		for (int i = 0; i < xPointsD.Count; i++)
		{
			chart.Series[1].Points.Add(new DataPoint(xPointsD[i], yPointsD[i], zPointsD[i]) { Color = System.Drawing.Color.Red });
		}

		return chart;
	}

	private Dictionary<string, Dictionary<string, Dictionary<string, object>>> LoadPoints(string filename = "./points/points_24.json")
	{
		var json = System.IO.File.ReadAllText(filename);
		return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, object>>>>(json);
	}

	public static void Main(string[] args)
	{
		// Example usage of the Simulator class
		Simulator sim = new Simulator();
		sim.Run(visualize: false);
		Chart chart = sim.DisplayDetectedFld(sim.visual_field, sim.sampler.PntsPool);
		chart.ChartAreas[0].AxisX.Title = "Horizontal";
		chart.ChartAreas[0].AxisY.Title = "Vertical";
		chart.ChartAreas[0].AxisZ.Title = "Intensity";
		chart.Dispose();
		Console.WriteLine("Counter: " + sim.sampler.CountSamples);

		List<double> delta = new List<double>();
		foreach (var id in sim.sampler.PntsPool.Keys)
		{
			double x1 = Convert.ToDouble(sim.visual_field[id]["intensity"]);
			double x2 = Convert.ToDouble(sim.sampler.PntsPool[id]["final_intensity"]);
			double d = x1 - x2;
			delta.Add(d);
		}

		Console.WriteLine("Delta: " + string.Join(", ", delta.OrderBy(d => d)));
	}
}

