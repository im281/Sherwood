// This file defines types used to illustrate the use of the decision forest
// library in simple 1D to 1D regression problems.

using System;
using System.Collections.Generic;
using System.Drawing;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  class RegressionTrainingContext : ITrainingContext<AxisAlignedFeatureResponse, LinearFitAggregator1d>
  {
    #region Implementation of ITrainingContext
    public Object UserData { get { return null; } }

    public AxisAlignedFeatureResponse GetRandomFeature(Random random)
    {
      return new AxisAlignedFeatureResponse(0); // not actually random because only one feature possible  in 1D
    }

    public LinearFitAggregator1d GetStatisticsAggregator()
    {
      return new LinearFitAggregator1d();
    }

    public double ComputeInformationGain(LinearFitAggregator1d allStatistics, LinearFitAggregator1d leftStatistics, LinearFitAggregator1d rightStatistics)
    {
      double entropyBefore = ((LinearFitAggregator1d)(allStatistics)).Entropy();

      LinearFitAggregator1d leftLineFitStats = (LinearFitAggregator1d)(leftStatistics);
      LinearFitAggregator1d rightLineFitStatistics = (LinearFitAggregator1d)(rightStatistics);

      int nTotalSamples = leftLineFitStats.SampleCount + rightLineFitStatistics.SampleCount;

      double entropyAfter = (leftLineFitStats.SampleCount * leftLineFitStats.Entropy() + rightLineFitStatistics.SampleCount * rightLineFitStatistics.Entropy()) / nTotalSamples;

      return entropyBefore - entropyAfter;
    }

    public bool ShouldTerminate(LinearFitAggregator1d parent, LinearFitAggregator1d leftChild, LinearFitAggregator1d rightChild, double gain)
    {
      return gain < 0.05;
    }
    #endregion
  }

  class RegressionExample
  {
    public Size PlotSize = new Size(300, 300);
    public PointF PlotDilation = new PointF(0.1f, 0.1f);

    public Color DensityColor = Color.FromArgb(255, 194, 32, 14);
    public Color DataPointColor = Color.LightGray;
    public Color DataPointBorderColor = Color.Black;
    public Color MeanColor = Color.Green;

    public TrainingParameters TrainingParameters = new TrainingParameters()
    {
      MaxDecisionLevels = 2,
      NumberOfCandidateThresholdsPerFeature = 1,
      NumberOfCandidateFeatures = 100,
      NumberOfTrees = 10
    };

    public Bitmap Run(DataPointCollection trainingData)
    {
      // Train the forest
      Console.WriteLine("Training the forest...");

      Random random = new Random();
      ITrainingContext<AxisAlignedFeatureResponse, LinearFitAggregator1d> regressionTrainingContext = new RegressionTrainingContext();

      var forest = ForestTrainer<AxisAlignedFeatureResponse, LinearFitAggregator1d>.TrainForest(
          random,
          TrainingParameters,
          regressionTrainingContext,
          trainingData);

      // Generate some test samples in a grid pattern (a useful basis for creating visualization images)
      PlotCanvas plotCanvas = new PlotCanvas(trainingData.GetRange(0), trainingData.GetTargetRange(), PlotSize, PlotDilation);

      DataPointCollection testData = DataPointCollection.Generate1dGrid(plotCanvas.plotRangeX, PlotSize.Width);

      // Apply the trained forest to the test data
      Console.WriteLine("\nApplying the forest to test data...");

      int[][] leafNodeIndices = forest.Apply(testData);

      #region Generate Visualization Image
      Bitmap result = new Bitmap(PlotSize.Width, PlotSize.Height);

      // Plot the learned density
      Color inverseDensityColor = Color.FromArgb(255, 255 - DensityColor.R, 255 - DensityColor.G, 255 - DensityColor.B);

      double[] mean_y_given_x = new double[PlotSize.Width];

      int index = 0;
      for (int i = 0; i < PlotSize.Width; i++)
      {
        double totalProbability = 0.0;
        for (int j = 0; j < PlotSize.Height; j++)
        {
          // Map pixel coordinate (i,j) in visualization image back to point in input space
          float x = plotCanvas.plotRangeX.Item1 + i * plotCanvas.stepX;
          float y = plotCanvas.plotRangeY.Item1 + j * plotCanvas.stepY;

          double probability = 0.0;

          // Aggregate statistics for this sample over all trees
          for (int t = 0; t < forest.TreeCount; t++)
          {
            Node<AxisAlignedFeatureResponse, LinearFitAggregator1d> leafNodeCopy = forest.GetTree(t).GetNode(leafNodeIndices[t][i]);

            LinearFitAggregator1d leafStatistics = leafNodeCopy.TrainingDataStatistics;

            probability += leafStatistics.GetProbability(x, y);
          }

          probability /= forest.TreeCount;

          mean_y_given_x[i] += probability * y;
          totalProbability += probability;

          float scale = 10.0f * (float)probability;

          Color weightedColor = Color.FromArgb(
              255,
              (byte)(Math.Min(scale * inverseDensityColor.R + 0.5f, 255.0f)),
              (byte)(Math.Min(scale * inverseDensityColor.G + 0.5f, 255.0f)),
              (byte)(Math.Min(scale * inverseDensityColor.B + 0.5f, 255.0f)));

          Color c = Color.FromArgb(255, 255 - weightedColor.R, 255 - weightedColor.G, 255 - weightedColor.G);

          result.SetPixel(i, j, c);

          index++;
        }

        // NB We don't really compute the mean over y, just over the region of y that is plotted
        mean_y_given_x[i] /= totalProbability;
      }

      // Also plot the mean curve and the original training data
      using (Graphics g = Graphics.FromImage(result))
      {
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

        using (Pen meanPen = new Pen(MeanColor, 2))
        {
          for (int i = 0; i < PlotSize.Width - 1; i++)
          {
            g.DrawLine(
                meanPen,
                (float)(i),
                (float)((mean_y_given_x[i] - plotCanvas.plotRangeY.Item1) / plotCanvas.stepY),
                (float)(i + 1),
                (float)((mean_y_given_x[i + 1] - plotCanvas.plotRangeY.Item1) / plotCanvas.stepY));
          }
        }

        using (Brush dataPointBrush = new SolidBrush(DataPointColor))
        using (Pen dataPointBorderPen = new Pen(DataPointBorderColor))
        {
          for (int s = 0; s < trainingData.Count(); s++)
          {
            // Map sample coordinate back to a pixel coordinate in the visualization image
            PointF x = new PointF(
                (trainingData.GetDataPoint(s)[0] - plotCanvas.plotRangeX.Item1) / plotCanvas.stepX,
                (trainingData.GetTarget(s) - plotCanvas.plotRangeY.Item1) / plotCanvas.stepY);

            RectangleF rectangle = new RectangleF(x.X - 2.0f, x.Y - 2.0f, 4.0f, 4.0f);
            g.FillRectangle(dataPointBrush, rectangle);
            g.DrawRectangle(dataPointBorderPen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
          }
        }
      }

      return result;
      #endregion
    }
  }
}