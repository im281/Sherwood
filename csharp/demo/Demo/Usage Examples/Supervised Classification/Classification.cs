// This file defines types used to illustrate the use of the decision forest
// library in simple multi-class classification problems.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  interface IFeatureFactory<F>
  {
    F CreateRandom(Random random);
  }

  class ClassificationTrainingContext<F> : ITrainingContext<F, HistogramAggregator> where F : IFeatureResponse
  {
    int nClasses_;

    IFeatureFactory<F> featureFactory_;
    Random random_;

    public ClassificationTrainingContext(int nClasses, IFeatureFactory<F> featureFactory, Random random)
    {
      nClasses_ = nClasses;
      featureFactory_ = featureFactory;
      random_ = random;
    }

    #region Implementation of ITrainingContext

    public Object UserData { get { return null; } }

    public F GetRandomFeature(Random random)
    {
      return featureFactory_.CreateRandom(random_);
    }

    public HistogramAggregator GetStatisticsAggregator()
    {
      return new HistogramAggregator(nClasses_);
    }

    public double ComputeInformationGain(HistogramAggregator allStatistics, HistogramAggregator leftStatistics, HistogramAggregator rightStatistics)
    {
      double entropyBefore = allStatistics.Entropy();

      int nTotalSamples = leftStatistics.SampleCount + rightStatistics.SampleCount;

      if (nTotalSamples <= 1)
        return 0.0;

      double entropyAfter = (leftStatistics.SampleCount * leftStatistics.Entropy() + rightStatistics.SampleCount * rightStatistics.Entropy()) / nTotalSamples;

      return entropyBefore - entropyAfter;
    }

    public bool ShouldTerminate(HistogramAggregator parent, HistogramAggregator leftChild, HistogramAggregator rightChild, double gain)
    {
      return gain < 0.01;
    }
    #endregion
  }

  public enum FeatureType
  {
    AxisAligned,
    Linear
  };

  class LinearFeatureFactory : IFeatureFactory<LinearFeatureResponse2d>
  {
    public LinearFeatureResponse2d CreateRandom(Random random)
    {
      return LinearFeatureResponse2d.CreateRandom(random);
    }
  }

  class AxisAlignedFeatureFactory : IFeatureFactory<AxisAlignedFeatureResponse>
  {
    public AxisAlignedFeatureResponse CreateRandom(Random random)
    {
      return AxisAlignedFeatureResponse.CreateRandom(random);
    }
  }

  class ClassificationExample
  {
    static public Forest<F, HistogramAggregator> Train<F>(
        DataPointCollection trainingData,
        IFeatureFactory<F> featureFactory,
        TrainingParameters TrainingParameters) where F : IFeatureResponse
    {
      if (trainingData.Dimensions != 2)
        throw new Exception("Training data points must be 2D.");
      if (trainingData.HasLabels == false)
        throw new Exception("Training data points must be labelled.");
      if (trainingData.HasTargetValues == true)
        throw new Exception("Training data points should not have target values.");

      Console.WriteLine("Running training...");

      Random random = new Random();
      ITrainingContext<F, HistogramAggregator> classificationContext =
          new ClassificationTrainingContext<F>(trainingData.CountClasses(), featureFactory, random);

      var forest = ForestTrainer<F, HistogramAggregator>.TrainForest(
          random,
          TrainingParameters,
          classificationContext,
          trainingData);

      return forest;
    }

    public static Bitmap Visualize<F>(
        Forest<F, HistogramAggregator> forest,
        DataPointCollection trainingData,
        Size PlotSize,
        PointF PlotDilation) where F : IFeatureResponse
    {
      // Size PlotSize = new Size(300, 300), PointF PlotDilation = new PointF(0.1f, 0.1f)
      // Generate some test samples in a grid pattern (a useful basis for creating visualization images)
      PlotCanvas plotCanvas = new PlotCanvas(trainingData.GetRange(0), trainingData.GetRange(1), PlotSize, PlotDilation);

      DataPointCollection testData = DataPointCollection.Generate2dGrid(plotCanvas.plotRangeX, PlotSize.Width, plotCanvas.plotRangeY, PlotSize.Height);

      Console.WriteLine("\nApplying the forest to test data...");
      int[][] leafNodeIndices = forest.Apply(testData);

      // Form a palette of random colors, one per class
      Color[] colors = new Color[Math.Max(trainingData.CountClasses(), 4)];

      // First few colours are same as those in the book, remainder are random.
      colors[0] = Color.FromArgb(183, 170, 8);
      colors[1] = Color.FromArgb(194, 32, 14);
      colors[2] = Color.FromArgb(4, 154, 10);
      colors[3] = Color.FromArgb(13, 26, 188);

      Color grey = Color.FromArgb(255, 127, 127, 127);

      System.Random r = new Random(0); // same seed every time so colours will be consistent
      for (int c = 4; c < colors.Length; c++)
        colors[c] = Color.FromArgb(255, r.Next(0, 255), r.Next(0, 255), r.Next(0, 255));

      // Create a visualization image
      Bitmap result = new Bitmap(PlotSize.Width, PlotSize.Height);

      // For each pixel...
      int index = 0;
      for (int j = 0; j < PlotSize.Height; j++)
      {
        for (int i = 0; i < PlotSize.Width; i++)
        {
          // Aggregate statistics for this sample over all leaf nodes reached
          HistogramAggregator h = new HistogramAggregator(trainingData.CountClasses());
          for (int t = 0; t < forest.TreeCount; t++)
          {
            int leafIndex = leafNodeIndices[t][index];
            h.Aggregate(forest.GetTree(t).GetNode(leafIndex).TrainingDataStatistics);
          }

          // Let's muddy the colors with grey where the entropy is high.
          float mudiness = 0.5f * (float)(h.Entropy());

          float R = 0.0f, G = 0.0f, B = 0.0f;

          for (int b = 0; b < trainingData.CountClasses(); b++)
          {
            float p = (1.0f - mudiness) * h.GetProbability(b); // NB probabilities sum to 1.0 over the classes

            R += colors[b].R * p;
            G += colors[b].G * p;
            B += colors[b].B * p;
          }

          R += grey.R * mudiness;
          G += grey.G * mudiness;
          B += grey.B * mudiness;

          Color c = Color.FromArgb(255, (byte)(R), (byte)(G), (byte)(B));

          result.SetPixel(i, j, c); // painfully slow but safe

          index++;
        }
      }

      // Also draw the original training data
      using (Graphics g = Graphics.FromImage(result))
      {
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

        for (int s = 0; s < trainingData.Count(); s++)
        {
          PointF x = new PointF(
              (trainingData.GetDataPoint(s)[0] - plotCanvas.plotRangeX.Item1) / plotCanvas.stepX,
              (trainingData.GetDataPoint(s)[1] - plotCanvas.plotRangeY.Item1) / plotCanvas.stepY);

          RectangleF rectangle = new RectangleF(x.X - 3.0f, x.Y - 3.0f, 6.0f, 6.0f);
          g.FillRectangle(new SolidBrush(colors[trainingData.GetIntegerLabel(s)]), rectangle);
          g.DrawRectangle(new Pen(Color.Black), rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }
      }

      return result;
    }

    /// <summary>
    /// Apply a trained forest to some test data.
    /// </summary>
    /// <typeparam name="F">Type of split function</typeparam>
    /// <param name="forest">Trained forest</param>
    /// <param name="testData">Test data</param>
    /// <returns>An array of class distributions, one per test data point</returns>
    public static HistogramAggregator[] Test<F>(Forest<F, HistogramAggregator> forest, DataPointCollection testData) where F : IFeatureResponse
    {
      int nClasses = forest.GetTree(0).GetNode(0).TrainingDataStatistics.BinCount;

      int[][] leafIndicesPerTree = forest.Apply(testData);

      HistogramAggregator[] result = new HistogramAggregator[testData.Count()];

      for (int i = 0; i < testData.Count(); i++)
      {
        // Aggregate statistics for this sample over all leaf nodes reached
        result[i] = new HistogramAggregator(nClasses);
        for (int t = 0; t < forest.TreeCount; t++)
        {
          int leafIndex = leafIndicesPerTree[t][i];
          result[i].Aggregate(forest.GetTree(t).GetNode(leafIndex).TrainingDataStatistics);
        }
      }

      return result;
    }
  }
}