using System;
using System.Collections.Generic;
using System.Drawing;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  // This file defines types used to illustrate the use of the decision forest
  // library in a simple 2D density estimation problems.

  class SemiSupervisedClassificationTrainingContext : ITrainingContext<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator>
  {
    // In semi-supervised training, we define information gain as a weighted
    // sum of supervised and unsupervised terms. This parameter describes the
    // importance of the unsupervised term relative to the supervised one.
    // For more information see:
    //  "A. Criminisi and J. Shotton, Decision Forests: for Computer Vision and
    //  Medical Image Analysis. Springer, 2013"
    const double alpha_ = 1.0;

    int nClasses_;
    double a_, b_; // hyperparameters of prior used for density estimation

    public SemiSupervisedClassificationTrainingContext(int nClasses, Random random, double a = 10, double b = 400)
    {
      a_ = a;
      b_ = b;
      nClasses_ = nClasses;
    }

    #region Implementation of ITrainingContext
    public Object UserData { get { return null; } }

    public LinearFeatureResponse2d GetRandomFeature(Random random)
    {
      return new LinearFeatureResponse2d((float)(2.0 * random.NextDouble() - 1.0), (float)(2.0 * random.NextDouble() - 1.0));
    }

    public SemiSupervisedClassificationStatisticsAggregator GetStatisticsAggregator()
    {
      return new SemiSupervisedClassificationStatisticsAggregator(nClasses_, a_, b_);
    }

    public double ComputeInformationGain(SemiSupervisedClassificationStatisticsAggregator allStatistics, SemiSupervisedClassificationStatisticsAggregator leftStatistics, SemiSupervisedClassificationStatisticsAggregator rightStatistics)
    {
      double informationGainLabelled;
      {
        double entropyBefore = allStatistics.HistogramAggregator.Entropy();

        HistogramAggregator leftHistogram = leftStatistics.HistogramAggregator;
        HistogramAggregator rightHistogram = rightStatistics.HistogramAggregator;

        int nTotalSamples = leftHistogram.SampleCount + rightHistogram.SampleCount;

        if (nTotalSamples <= 1)
        {
          informationGainLabelled = 0;
        }
        else
        {
          double entropyAfter = (leftHistogram.SampleCount * leftHistogram.Entropy() + rightHistogram.SampleCount * rightHistogram.Entropy()) / nTotalSamples;

          informationGainLabelled = entropyBefore - entropyAfter;
        }
      }

      double informationGainUnlabelled;
      {
        double entropyBefore = ((SemiSupervisedClassificationStatisticsAggregator)(allStatistics)).GaussianAggregator2d.GetPdf().Entropy();

        GaussianAggregator2d leftGaussian = leftStatistics.GaussianAggregator2d;
        GaussianAggregator2d rightGaussian = rightStatistics.GaussianAggregator2d;

        int nTotalSamples = leftGaussian.SampleCount + rightGaussian.SampleCount;

        double entropyAfter = (leftGaussian.SampleCount * leftGaussian.GetPdf().Entropy() + rightGaussian.SampleCount * rightGaussian.GetPdf().Entropy()) / nTotalSamples;

        informationGainUnlabelled = entropyBefore - entropyAfter;
      }

      double gain =
          informationGainLabelled
          + alpha_ * informationGainUnlabelled;

      return gain;
    }

    public bool ShouldTerminate(SemiSupervisedClassificationStatisticsAggregator parent, SemiSupervisedClassificationStatisticsAggregator leftChild, SemiSupervisedClassificationStatisticsAggregator rightChild, double gain)
    {
      return gain < 0.4;
    }
    #endregion
  }

  class SemiSupervisedClassificationExample
  {
    static Color UnlabelledDataPointColor = Color.FromArgb(255, 192, 192, 192);

    static double LuminanceScaleFactor = 2000000.0;

    public static Forest<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator> Train(
        DataPointCollection trainingData,
        TrainingParameters parameters,
        double a_,
        double b_)
    {
      // Train the forest
      Console.WriteLine("Training the forest...");

      Random random = new Random();

      ITrainingContext<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator> classificationContext
          = new SemiSupervisedClassificationTrainingContext(trainingData.CountClasses(), random, a_, b_);
      var forest = ForestTrainer<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator>.TrainForest(
           random,
           parameters,
           classificationContext,
           trainingData);

      // Label transduction to unlabelled leaves from nearest labelled leaf
      List<int> unlabelledLeafIndices = null;
      List<int> labelledLeafIndices = null;
      int[] closestLabelledLeafIndices = null;
      List<int> leafIndices = null;

      for (int t = 0; t < forest.TreeCount; t++)
      {
        var tree = forest.GetTree(t);
        leafIndices = new List<int>();

        unlabelledLeafIndices = new List<int>();
        labelledLeafIndices = new List<int>();

        for (int n = 0; n < tree.NodeCount; n++)
        {
          if (tree.GetNode(n).IsLeaf)
          {
            if (tree.GetNode(n).TrainingDataStatistics.HistogramAggregator.SampleCount == 0)
              unlabelledLeafIndices.Add(leafIndices.Count);
            else
              labelledLeafIndices.Add(leafIndices.Count);

            leafIndices.Add(n);
          }
        }

        // Build an upper triangular matrix of inter-leaf distances
        float[,] interLeafDistances = new float[leafIndices.Count, leafIndices.Count];
        for (int i = 0; i < leafIndices.Count; i++)
        {
          for (int j = i + 1; j < leafIndices.Count; j++)
          {
            SemiSupervisedClassificationStatisticsAggregator a = tree.GetNode(leafIndices[i]).TrainingDataStatistics;
            SemiSupervisedClassificationStatisticsAggregator b = tree.GetNode(leafIndices[j]).TrainingDataStatistics;
            GaussianPdf2d x = a.GaussianAggregator2d.GetPdf();
            GaussianPdf2d y = b.GaussianAggregator2d.GetPdf();

            interLeafDistances[i, j] = (float)(Math.Max(
                  x.GetNegativeLogProbability((float)(y.MeanX), (float)(y.MeanY)),
                +y.GetNegativeLogProbability((float)(x.MeanX), (float)(x.MeanY))));
          }
        }

        // Find shortest paths between all pairs of nodes in the graph of leaf nodes
        FloydWarshall pathFinder = new FloydWarshall(interLeafDistances);

        // Find the closest labelled leaf to each unlabelled leaf
        float[] minDistances = new float[unlabelledLeafIndices.Count];
        closestLabelledLeafIndices = new int[unlabelledLeafIndices.Count];
        for (int i = 0; i < minDistances.Length; i++)
        {
          minDistances[i] = float.PositiveInfinity;
          closestLabelledLeafIndices[i] = -1; // unused so deliberately invalid
        }

        for (int l = 0; l < labelledLeafIndices.Count; l++)
        {
          for (int u = 0; u < unlabelledLeafIndices.Count; u++)
          {
            if (pathFinder.GetMinimumDistance(unlabelledLeafIndices[u], labelledLeafIndices[l]) < minDistances[u])
            {
              minDistances[u] = pathFinder.GetMinimumDistance(unlabelledLeafIndices[u], labelledLeafIndices[l]);
              closestLabelledLeafIndices[u] = leafIndices[labelledLeafIndices[l]];
            }
          }
        }

        // Propagate class probability distributions to each unlabelled
        // leaf from its nearest labelled leaf.
        for (int u = 0; u < unlabelledLeafIndices.Count; u++)
        {
          // Unhelpfully, C# only allows us to pass value types by value
          // so Tree.GetNode() returns only a COPY of the Node. We update
          // this copy and then copy it back over the top of the
          // original via Tree.SetNode().

          // The C++ version is a lot better!

          var unlabelledLeafCopy = tree.GetNode(leafIndices[unlabelledLeafIndices[u]]);
          var labelledLeafCopy = tree.GetNode(closestLabelledLeafIndices[u]);

          unlabelledLeafCopy.TrainingDataStatistics.HistogramAggregator
             = (HistogramAggregator)(labelledLeafCopy.TrainingDataStatistics.HistogramAggregator.DeepClone());

          tree.SetNode(leafIndices[unlabelledLeafIndices[u]], unlabelledLeafCopy);
        }
      }

      return forest;
    }

    public static Bitmap VisualizeLabels(Forest<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator> forest, DataPointCollection trainingData, Size PlotSize, PointF PlotDilation)
    {
      // Generate some test samples in a grid pattern (a useful basis for creating visualization images)
      PlotCanvas plotCanvas = new PlotCanvas(trainingData.GetRange(0), trainingData.GetRange(1), PlotSize, PlotDilation);

      // Apply the trained forest to the test data
      Console.WriteLine("\nApplying the forest to test data...");

      DataPointCollection testData = DataPointCollection.Generate2dGrid(plotCanvas.plotRangeX, PlotSize.Width, plotCanvas.plotRangeY, PlotSize.Height);

      int[][] leafNodeIndices = forest.Apply(testData);

      Bitmap result = new Bitmap(PlotSize.Width, PlotSize.Height);

      // Paint the test data
      GaussianPdf2d[][] leafDistributions = new GaussianPdf2d[forest.TreeCount][];
      for (int t = 0; t < forest.TreeCount; t++)
      {
        leafDistributions[t] = new GaussianPdf2d[forest.GetTree(t).NodeCount];
        for (int i = 0; i < forest.GetTree(t).NodeCount; i++)
        {
          Node<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator> nodeCopy = forest.GetTree(t).GetNode(i);

          if (nodeCopy.IsLeaf)
            leafDistributions[t][i] = nodeCopy.TrainingDataStatistics.GaussianAggregator2d.GetPdf();
        }
      }

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

            SemiSupervisedClassificationStatisticsAggregator a = forest.GetTree(t).GetNode(leafIndex).TrainingDataStatistics;

            h.Aggregate(a.HistogramAggregator);
          }

          // Let's muddy the colors with a little grey where entropy is high.
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

          result.SetPixel(i, j, c);

          index++;
        }
      }

      PaintTrainingData(trainingData, plotCanvas, result);

      return result;
    }

    public static Bitmap VisualizeDensity(Forest<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator> forest, DataPointCollection trainingData, Size PlotSize, PointF PlotDilation)
    {
      // Generate some test samples in a grid pattern (a useful basis for creating visualization images)
      PlotCanvas plotCanvas = new PlotCanvas(trainingData.GetRange(0), trainingData.GetRange(1), PlotSize, PlotDilation);

      // Apply the trained forest to the test data
      Console.WriteLine("\nApplying the forest to test data...");

      DataPointCollection testData = DataPointCollection.Generate2dGrid(plotCanvas.plotRangeX, PlotSize.Width, plotCanvas.plotRangeY, PlotSize.Height);

      int[][] leafNodeIndices = forest.Apply(testData);

      Bitmap result = new Bitmap(PlotSize.Width, PlotSize.Height);

      // Paint the test data
      int index = 0;
      for (int j = 0; j < PlotSize.Height; j++)
      {
        for (int i = 0; i < PlotSize.Width; i++)
        {
          // Map pixel coordinate (i,j) in visualization image back to point in input space
          float x = plotCanvas.plotRangeX.Item1 + i * plotCanvas.stepX;
          float y = plotCanvas.plotRangeY.Item1 + j * plotCanvas.stepY;

          // Aggregate statistics for this sample over all trees
          double probability = 0.0;
          for (int t = 0; t < forest.TreeCount; t++)
          {
            int leafIndex = leafNodeIndices[t][index];
            probability += forest.GetTree(t).GetNode(leafIndex).TrainingDataStatistics.GaussianAggregator2d.GetPdf().GetProbability(x, y);
          }

          probability /= forest.TreeCount;

          float l = (float)(LuminanceScaleFactor * probability);

          if (l < 0)
            l = 0;
          else if (l > 255)
            l = 255;

          Color c = Color.FromArgb(255, (byte)(l), 0, 0);
          result.SetPixel(i, j, c);

          index++;
        }
      }

      PaintTrainingData(trainingData, plotCanvas, result);

      return result;
    }

    static void PaintTrainingData(DataPointCollection trainingData, PlotCanvas plotCanvas, Bitmap result)
    {
      // First few colours are same as those in the book, remainder are random.
      Color[] colors = new Color[Math.Max(trainingData.CountClasses(), 4)];
      colors[0] = Color.FromArgb(183, 170, 8);
      colors[1] = Color.FromArgb(194, 32, 14);
      colors[2] = Color.FromArgb(4, 154, 10);
      colors[3] = Color.FromArgb(13, 26, 188);

      System.Random r = new Random(0); // same seed every time so colours will be consistent
      for (int c = 4; c < colors.Length; c++)
        colors[c] = Color.FromArgb(255, r.Next(0, 255), r.Next(0, 255), r.Next(0, 255));

      // Also plot the original training data (a little bigger for clarity)
      using (Graphics g = Graphics.FromImage(result))
      {
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

        // Paint unlabelled data
        for (int s = 0; s < trainingData.Count(); s++)
        {
          if (trainingData.GetIntegerLabel(s) == DataPointCollection.UnknownClassLabel) // unlabelled
          {
            PointF x = new PointF(
                (trainingData.GetDataPoint(s)[0] - plotCanvas.plotRangeX.Item1) / plotCanvas.stepX,
                (trainingData.GetDataPoint(s)[1] - plotCanvas.plotRangeY.Item1) / plotCanvas.stepY);

            RectangleF rectangle = new RectangleF(x.X - 2.0f, x.Y - 2.0f, 4.0f, 4.0f);
            g.FillRectangle(new SolidBrush(UnlabelledDataPointColor), rectangle);
            g.DrawRectangle(new Pen(Color.Black), rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
          }
        }

        // Paint labelled data on top
        for (int s = 0; s < trainingData.Count(); s++)
        {
          if (trainingData.GetIntegerLabel(s) != DataPointCollection.UnknownClassLabel)
          {
            PointF x = new PointF(
                (trainingData.GetDataPoint(s)[0] - plotCanvas.plotRangeX.Item1) / plotCanvas.stepX,
                (trainingData.GetDataPoint(s)[1] - plotCanvas.plotRangeY.Item1) / plotCanvas.stepY);

            RectangleF rectangle = new RectangleF(x.X - 5.0f, x.Y - 5.0f, 10.0f, 10.0f);
            g.FillRectangle(new SolidBrush(colors[trainingData.GetIntegerLabel(s)]), rectangle);
            g.DrawRectangle(new Pen(Color.White, 2), rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

          }
        }

      }
    }
  }
}