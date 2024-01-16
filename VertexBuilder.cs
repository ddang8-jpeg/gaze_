using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

public class VertexBuilder
{
	private const double A_LIMIT_24 = 24;
	private const double A_LIMIT_30 = 30;

	private readonly List<(double, double)> hexagonVertices = new List<(double, double)>();

	public VertexBuilder()
	{
		InitializeHexagonVertices();

		// Create a figure and axis, setting up the initial plot
		var fig = new Figure();
		var ax = fig.AddSubplot(111);
		ax.SetEqual();
		ax.SetAxisLimits(-31, 31, -31, 31);
		ax.Axhline(0, Color.Black);
		ax.Axvline(0, Color.Black);
		ax.Grid();

		// Add dashed circles representing A_LIMIT_24 and A_LIMIT_30
		var circle1 = new Circle((0, 0), A_LIMIT_24, Color.Red, linestyle: LineStyle.Dashed, fill: false);
		var circle2 = new Circle((0, 0), A_LIMIT_30, Color.Red, linestyle: LineStyle.Dashed, fill: false);
		ax.AddPatch(circle1);
		ax.AddPatch(circle2);
	}

	public Dictionary<string, Dictionary<string, object>> FillArea24(bool plot = true, bool save = false)
	{
		// Parameters for hexagon generation
		double h = 4.75;
		double side = h * 2 / Math.Sqrt(3);

		// Generate points in quadrant 1
		var q1Points = new List<(double, double)>();
		for (int i = 0; i < Math.Ceiling((A_LIMIT_24 - h / 2) / h); i++)
		{
			q1Points.AddRange(Enumerable.Range(0, (int)A_LIMIT_24)
																	.Select(x => (x + side * (i % 2) / 2, h / 2 + i * h))
																	.Where(p => p.Item1 * p.Item1 + p.Item2 * p.Item2 < A_LIMIT_24 * A_LIMIT_24));
		}

		q1Points = q1Points.Where(p => p.Item1 * p.Item1 + p.Item2 * p.Item2 < A_LIMIT_24 * A_LIMIT_24).ToList();

		// Generate points in other quadrants
		var q2Points = q1Points.Select(p => (-p.Item1, p.Item2)).ToList();
		var q3Points = q1Points.Select(p => (-p.Item1, -p.Item2)).ToList();
		var q4Points = q1Points.Select(p => (p.Item1, -p.Item2)).ToList();

		var points = q1Points.Concat(q2Points).Concat(q3Points).Concat(q4Points).ToList();
		points = points.Distinct().ToList();

		var pointsDict = BuildOutputDict(points, side, A_LIMIT_24);

		if (save)
		{
			var json = JsonConvert.SerializeObject(pointsDict, Formatting.Indented);
			File.WriteAllText($"./points/points_{A_LIMIT_24}.json", json);
		}

		if (plot)
		{
			var scatterPoints = pointsDict["tier_1"].Values.Select(p => (double)((Tuple<double, double>)p["point"]).Item1).ToList();
			var scatterPoints2 = pointsDict["tier_1"].Values.Select(p => (double)((Tuple<double, double>)p["point"]).Item2).ToList();
			// Plot the points using the specified plotting library or method
			// For simplicity, let's assume a fictional Plot method is used here.
			Plot(scatterPoints, scatterPoints2, Color.Blue);
		}

		return pointsDict;
	}

	private void InitializeHexagonVertices()
	{
		// Compute vertices of a regular hexagon centered at (0, 0)
		for (double theta = 0; theta < 2 * Math.PI; theta += Math.PI / 3)
		{
			hexagonVertices.Add((Math.Cos(theta), Math.Sin(theta)));
		}
	}

	private Dictionary<string, Dictionary<string, object>> BuildOutputDict(List<(double, double)> points, double side, double aLimit)
	{
		var pointsDict = new Dictionary<string, Dictionary<string, object>>();
		pointsDict["tier_1"] = new Dictionary<string, object>();
		pointsDict["tier_2"] = new Dictionary<string, object>();

		foreach (var p in points)
		{
			var t2 = hexagonVertices.Select(x => (x.Item1 * side / 2 + p.Item1, x.Item2 * side / 2 + p.Item2))
															.Where(t => t.Item1 * t.Item1 + t.Item2 * t.Item2 < A_LIMIT_24 * A_LIMIT_24)
															.ToList();

			var t2Ids = new List<string>();
			foreach (var t in t2)
			{
				var tId = GetId(t, aLimit);
				pointsDict["tier_2"][tId] = new Dictionary<string, object>
								{
										{ "point", (Math.Round(t.Item1, 2), Math.Round(t.Item2, 2)) }
								};
				t2Ids.Add(tId);
			}

			var nTier1 = points.Where(x => Math.Sqrt((x.Item1 - p.Item1) * (x.Item1 - p.Item1) + (x.Item2 - p.Item2) * (x.Item2 - p.Item2)) <= 1.5 * side && x != p)
												.Select(x => GetId(x, aLimit))
												.ToList();

			pointsDict["tier_1"][GetId(p, aLimit)] = new Dictionary<string, object>
						{
								{ "point", (Math.Round(p.Item1, 2), Math.Round(p.Item2, 2)) },
								{ "n_tier_2", t2Ids },
								{ "n_tier_1", nTier1 }
						};
		}

		return pointsDict;
	}

	private static string GetId((double, double) point, double aLimit)
	{
		return $"{(int)((aLimit + Math.Round(point.Item2, 2)) * 10000000 + 100 * Math.Round(point.Item1, 2))}";
	}

	// The Plot method needs to be implemented based on the specific plotting library or method used in C#
	private void Plot(List<double> x, List<double> y, Color color)
	{
		// Implementation of plotting method goes here
	}
}

// The following classes are simplified placeholders for the actual implementations in your plotting library
public class Figure
{
	public Subplot AddSubplot(int index)
	{
		return new Subplot();
	}
}

public class Subplot
{
	public void SetEqual()
	{
		// Implementation goes here
	}

	public void SetAxisLimits(double xMin, double xMax, double yMin, double yMax)
	{
		// Implementation goes here
	}

	public void Axhline(double y, Color color)
	{
		// Implementation goes here
	}

	public void Axvline(double x, Color color)
	{
		// Implementation goes here
	}

	public void Grid()
	{
		// Implementation goes here
	}

	public void AddPatch(Circle circle)
	{
		// Implementation goes here
	}
}

public class Circle
{
	public (double, double) Center { get; }
	public double Radius { get; }
	public Color FillColor { get; }
	public LineStyle LineStyle { get; }

	public Circle((double, double) center, double radius, Color fillColor, LineStyle lineStyle, bool fill)
	{
		Center = center;
		Radius = radius;
		FillColor = fillColor;
		LineStyle = lineStyle;
		// Additional properties go here
	}
}

public class Color
{
	// Implementation goes here
}

public enum LineStyle
{
	Solid,
	Dashed
}

// Additional classes and enums go here as needed
