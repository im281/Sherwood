using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  // This file defines types used to illustrate the use of the decision forest
  // library in a simple 2D density estimation problems.

  class DensityEstimationTrainingContext : ITrainingContext<AxisAlignedFeatureResponse, GaussianAggregator2d>
  {
    double a_, b_;

    public DensityEstimationTrainingContext(double a, double b)
    {
      a_ = a;
      b_ = b;
    }

    #region Implementation of ITrainingContext
    public Object UserData { get { return null; } }

    public AxisAlignedFeatureResponse GetRandomFeature(Random random)
    {
      return new AxisAlignedFeatureResponse(random.Next(0, 2));
    }

    public GaussianAggregator2d GetStatisticsAggregator()
    {
      return new GaussianAggregator2d(a_, b_);
    }

    public double ComputeInformationGain(GaussianAggregator2d allStatistics, GaussianAggregator2d leftStatistics, GaussianAggregator2d rightStatistics)
    {
      double entropyBefore = ((GaussianAggregator2d)(allStatistics)).GetPdf().Entropy();

      GaussianAggregator2d leftGaussian = (GaussianAggregator2d)(leftStatistics);
      GaussianAggregator2d rightGaussian = (GaussianAggregator2d)(rightStatistics);

      int nTotalSamples = leftGaussian.SampleCount + rightGaussian.SampleCount;

      double entropyAfter = (leftGaussian.SampleCount * leftGaussian.GetPdf().Entropy() + rightGaussian.SampleCount * rightGaussian.GetPdf().Entropy()) / nTotalSamples;

      return entropyBefore - entropyAfter;
    }

    public bool ShouldTerminate(GaussianAggregator2d parent, GaussianAggregator2d leftChild, GaussianAggregator2d rightChild, double gain)
    {
      return gain < 0.25;
    }
    #endregion
  }

  class DensityEstimationExample
  {
    static Color DataPointColor = Color.FromArgb(255, 0, 255, 0);
    static double Gamma = 0.333;
    static double LuminanceScaleFactor = 5000.0;

    class Bounds
    {
      public Bounds(int dimension)
      {
        Lower = new float[dimension];
        Upper = new float[dimension];

        for (int i = 0; i < dimension; i++)
        {
          Lower[i] = float.NegativeInfinity;
          Upper[i] = float.PositiveInfinity;
        }
      }

      public float[] Lower;
      public float[] Upper;

      public Bounds Clone()
      {
        Bounds b = new Bounds(Lower.Length);
        b.Lower = (float[])(Lower.Clone());
        b.Upper = (float[])(Upper.Clone());

        return b;
      }

      public override string ToString()
      {
        StringBuilder b = new StringBuilder();
        b.Append("(");
        for (int i = 0; i < Lower.Length; i++)
        {
          if (i != 0)
            b.Append(", ");
          b.Append(Lower[i]);
        }
        b.Append(") -> (");
        for (int i = 0; i < Lower.Length; i++)
        {
          if (i != 0)
            b.Append(", ");
          b.Append(Upper[i]);
        }
        b.Append(")");
        return b.ToString();
      }
    }

    static void ComputeNormalizationFactorsRecurse(
        Tree<AxisAlignedFeatureResponse, GaussianAggregator2d> t,
        int nodeIndex,
        int nTrainingPoints,
        Bounds bounds,
        double[] normalizationFactors)
    {
      GaussianPdf2d g = t.GetNode(nodeIndex).TrainingDataStatistics.GetPdf();

      // Evaluate integral of bivariate normal distribution within this node's bounds
      double u = CumulativeNormalDistribution2d.M(
          (bounds.Upper[0] - g.MeanX) / Math.Sqrt(g.VarianceX),
          (bounds.Upper[1] - g.MeanY) / Math.Sqrt(g.VarianceY),
          g.CovarianceXY / Math.Sqrt(g.VarianceX * g.VarianceY));

      double l = CumulativeNormalDistribution2d.M(
          (bounds.Lower[0] - g.MeanX) / Math.Sqrt(g.VarianceX),
          (bounds.Lower[1] - g.MeanY) / Math.Sqrt(g.VarianceY),
          g.CovarianceXY / Math.Sqrt(g.VarianceX * g.VarianceY));

      normalizationFactors[nodeIndex] = (double)(t.GetNode(nodeIndex).TrainingDataStatistics.SampleCount) / nTrainingPoints * 1.0 / (u - l);

      if (!t.GetNode(nodeIndex).IsLeaf)
      {
        Bounds leftChildBounds = bounds.Clone();
        leftChildBounds.Upper[t.GetNode(nodeIndex).Feature.Axis] = t.GetNode(nodeIndex).Threshold;
        ComputeNormalizationFactorsRecurse(t, nodeIndex * 2 + 1, nTrainingPoints, leftChildBounds, normalizationFactors);

        Bounds rightChildBounds = bounds.Clone();
        rightChildBounds.Lower[t.GetNode(nodeIndex).Feature.Axis] = t.GetNode(nodeIndex).Threshold;
        ComputeNormalizationFactorsRecurse(t, nodeIndex * 2 + 2, nTrainingPoints, rightChildBounds, normalizationFactors);
      }
    }

    static public Forest<AxisAlignedFeatureResponse, GaussianAggregator2d> Train(
        DataPointCollection trainingData,
        TrainingParameters parameters,
        double a,
        double b)
    {
      if (trainingData.Dimensions != 2)
        throw new Exception("Training data points for density estimation were not 2D.");
      if (trainingData.HasLabels == true)
        throw new Exception("Density estimation training data should not be labelled.");
      if (trainingData.HasTargetValues == true)
        throw new Exception("Training data should not have target values.");

      // Train the forest
      Console.WriteLine("Training the forest...");

      Random random = new Random();

      ITrainingContext<AxisAlignedFeatureResponse, GaussianAggregator2d> densityEstimationTrainingContext =
          new DensityEstimationTrainingContext(a, b);
      var forest = ForestTrainer<AxisAlignedFeatureResponse, GaussianAggregator2d>.TrainForest(
          random,
          parameters,
          densityEstimationTrainingContext,
          trainingData);

      return forest;

    }

    public static Bitmap Visualize(
        Forest<AxisAlignedFeatureResponse, GaussianAggregator2d> forest,
        DataPointCollection trainingData,
        Size PlotSize,
        PointF PlotDilation)
    {
      // Generate some test samples in a grid pattern (a useful basis for creating visualization images)
      PlotCanvas plotCanvas = new PlotCanvas(trainingData.GetRange(0), trainingData.GetRange(1), PlotSize, PlotDilation);

      // Apply the trained forest to the test data
      Console.WriteLine("\nApplying the forest to test data...");

      DataPointCollection testData = DataPointCollection.Generate2dGrid(plotCanvas.plotRangeX, PlotSize.Width, plotCanvas.plotRangeY, PlotSize.Height);

      int[][] leafNodeIndices = forest.Apply(testData);

      // Compute normalization factors per node
      int nTrainingPoints = (int)(trainingData.Count()); // could also count over tree nodes if training data no longer accessible
      double[][] normalizationFactors = new double[forest.TreeCount][];
      for (int t = 0; t < forest.TreeCount; t++)
      {
        normalizationFactors[t] = new double[forest.GetTree(t).NodeCount];
        ComputeNormalizationFactorsRecurse(forest.GetTree(t), 0, nTrainingPoints, new Bounds(2), normalizationFactors[t]);
      }

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

            probability += normalizationFactors[t][leafIndex] * forest.GetTree(t).GetNode(leafIndex).TrainingDataStatistics.GetPdf().GetProbability(x, y);
          }

          probability /= forest.TreeCount;

          // 'Gamma correct' probability density for better display
          float l = (float)(LuminanceScaleFactor * Math.Pow(probability, Gamma));

          if (l < 0)
            l = 0;
          else if (l > 255)
            l = 255;

          Color c = Color.FromArgb(255, (byte)(l), 0, 0);
          result.SetPixel(i, j, c);

          index++;
        }
      }

      // Also plot the original training data
      using (Graphics g = Graphics.FromImage(result))
      {
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

        for (int s = 0; s < trainingData.Count(); s++)
        {
          PointF x = new PointF(
              (trainingData.GetDataPoint(s)[0] - plotCanvas.plotRangeX.Item1) / plotCanvas.stepX,
              (trainingData.GetDataPoint(s)[1] - plotCanvas.plotRangeY.Item1) / plotCanvas.stepY);

          RectangleF rectangle = new RectangleF(x.X - 2.0f, x.Y - 2.0f, 4.0f, 4.0f);
          g.FillRectangle(new SolidBrush(DataPointColor), rectangle);
          g.DrawRectangle(new Pen(Color.Black), rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }
      }

      return result;
    }
  }
}