#pragma once

// This file defines types used to illustrate the use of the decision forest
// library in a simple 2D density estimation task.

#include <math.h>

#include <iostream>
#include <string>
#include <vector>
#include <sstream>
#include <limits>

#include "Graphics.h"

#include "Sherwood.h"

#include "CumulativeNormalDistribution.h"

#include "DataPointCollection.h"
#include "FeatureResponseFunctions.h"
#include "StatisticsAggregators.h"
#include "DensityEstimation.h"
#include "PlotCanvas.h"


namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  class DensityEstimationTrainingContext : public ITrainingContext<AxisAlignedFeatureResponse,GaussianAggregator2d>
  {
    double a_, b_;

  public:
    DensityEstimationTrainingContext(double a, double b)
    {
      a_ = a;
      b_ = b;
    }

    // Implementation of ITrainingContext
    AxisAlignedFeatureResponse GetRandomFeature(Random& random)
    {
      return AxisAlignedFeatureResponse(random.Next(0, 2));
    }

    GaussianAggregator2d GetStatisticsAggregator()
    {
      return GaussianAggregator2d(a_, b_);
    }

    double ComputeInformationGain(const GaussianAggregator2d& allStatistics, const GaussianAggregator2d& leftStatistics, const GaussianAggregator2d& rightStatistics)
    {
      double entropyBefore = ((GaussianAggregator2d)(allStatistics)).GetPdf().Entropy();

      GaussianAggregator2d leftGaussian = (GaussianAggregator2d)(leftStatistics);
      GaussianAggregator2d rightGaussian = (GaussianAggregator2d)(rightStatistics);

      unsigned int nTotalSamples = leftGaussian.SampleCount() + rightGaussian.SampleCount();

      double entropyAfter = (leftGaussian.SampleCount() * leftGaussian.GetPdf().Entropy() + rightGaussian.SampleCount() * rightGaussian.GetPdf().Entropy()) / nTotalSamples;

      return entropyBefore - entropyAfter;
    }

    bool ShouldTerminate(const GaussianAggregator2d& parent, const GaussianAggregator2d& leftChild, const GaussianAggregator2d& rightChild, double gain)
    {
      return gain < 0.25;
    }
  };

  class DensityEstimationExample
  {
  public:
    static const PixelBgr DataPointColor;
    static const double Gamma;
    static const double LuminanceScaleFactor;

    class Bounds
    {
    public:
      Bounds(int dimension)
      {
        Lower.resize(dimension);
        Upper.resize(dimension);

        for (int i = 0; i < dimension; i++)
        {
          Lower[i] = -std::numeric_limits<float>::infinity();
          Upper[i] = +std::numeric_limits<float>::infinity();
        }
      }

      std::vector<float> Lower;
      std::vector<float>  Upper;

      std::string ToString()
      {
        std::stringstream b;
        b << "(";
        for (std::vector<float>::size_type i = 0; i < Lower.size(); i++)
        {
          if (i != 0)
            b << ", ";
          b << Lower[i];
        }
        b << ") -> (";
        for (std::vector<float>::size_type  i = 0; i < Lower.size(); i++)
        {
          if (i != 0)
            b << ", ";
          b << Upper[i];
        }
        b << ")";
        return b.str();
      }
    };

    static void ComputeNormalizationFactorsRecurse(
      const Tree<AxisAlignedFeatureResponse, GaussianAggregator2d>& t,
      int nodeIndex,
      unsigned int nTrainingPoints,
      Bounds bounds,
      std::vector<double>& normalizationFactors)
    {
      Node<AxisAlignedFeatureResponse, GaussianAggregator2d> nodeCopy = t.GetNode(nodeIndex);

      // Evaluate integral of bivariate normal distribution within this nodes bounds
      const GaussianAggregator2d& aggregator = nodeCopy.TrainingDataStatistics;

      GaussianPdf2d g = aggregator.GetPdf();

      double u = CumulativeNormalDistribution2d::M(
        (bounds.Upper[0] - g.MeanX()) / sqrt(g.VarianceX()),
        (bounds.Upper[1] - g.MeanY()) / sqrt(g.VarianceY()),
        g.CovarianceXY() / sqrt(g.VarianceX() * g.VarianceY()));

      double l = CumulativeNormalDistribution2d::M(
        (bounds.Lower[0] - g.MeanX()) / sqrt(g.VarianceX()),
        (bounds.Lower[1] - g.MeanY()) / sqrt(g.VarianceY()),
        g.CovarianceXY() / sqrt(g.VarianceX() * g.VarianceY()));

      normalizationFactors[nodeIndex] = (double)(t.GetNode(nodeIndex).TrainingDataStatistics.SampleCount())/nTrainingPoints * 1.0/(u - l);

      if (!nodeCopy.IsLeaf())
      {
        AxisAlignedFeatureResponse feature = nodeCopy.Feature;

        Bounds leftChildBounds = bounds;
        leftChildBounds.Upper[feature.Axis()] = nodeCopy.Threshold;
        ComputeNormalizationFactorsRecurse(t, nodeIndex * 2 + 1, nTrainingPoints, leftChildBounds, normalizationFactors);

        Bounds rightChildBounds = bounds;
        rightChildBounds.Lower[feature.Axis()] = nodeCopy.Threshold;
        ComputeNormalizationFactorsRecurse(t, nodeIndex * 2 + 2, nTrainingPoints, rightChildBounds, normalizationFactors);
      }
    }

    static std::auto_ptr<Forest<AxisAlignedFeatureResponse, GaussianAggregator2d> > Train(
      const DataPointCollection& trainingData,
      const TrainingParameters& parameters,
      double a,
      double b)
    {
      if (trainingData.Dimensions() != 2)
        throw std::runtime_error("Training data points for density estimation were not 2D.");
      if (trainingData.HasLabels() == true)
        throw std::runtime_error("Density estimation training data should not be labelled.");
      if (trainingData.HasTargetValues() == true)
        throw std::runtime_error("Training data should not have target values.");

      std::cout << "Training the forest..." << std::endl;

      Random random;

      DensityEstimationTrainingContext densityEstimationTrainingContext(a, b);

      std::auto_ptr<Forest<AxisAlignedFeatureResponse, GaussianAggregator2d> > forest
        = ForestTrainer<AxisAlignedFeatureResponse, GaussianAggregator2d>::TrainForest (
        random,
        parameters,
        densityEstimationTrainingContext,
        trainingData );

      return forest;
    }

    static std::auto_ptr<Bitmap<PixelBgr> > Visualize(
      Forest<AxisAlignedFeatureResponse, GaussianAggregator2d>& forest,
      DataPointCollection& trainingData,
      Size PlotSize,
      PointF PlotDilation) 
    {
      // Generate some test samples in a grid pattern (a useful basis for creating visualization images)
      PlotCanvas plotCanvas(trainingData.GetRange(0), trainingData.GetRange(1), PlotSize, PlotDilation);

      std::cout << "\nApplying the forest to test data..." << std::endl;

      std::auto_ptr<DataPointCollection> testData = std::auto_ptr<DataPointCollection>(
        DataPointCollection::Generate2dGrid(plotCanvas.plotRangeX, PlotSize.Width, plotCanvas.plotRangeY, PlotSize.Height) );

      std::vector<std::vector<int> > leafNodeIndices;
      forest.Apply(*(testData.get()), leafNodeIndices);

      // Compute normalization factors per node
      unsigned int nTrainingPoints = trainingData.Count(); // could also count over tree nodes if training data no longer accessible
      std::vector<std::vector<double> > normalizationFactors(forest.TreeCount());
      for (int t = 0; t < forest.TreeCount(); t++)
      {
        normalizationFactors[t].resize(forest.GetTree((t)).NodeCount());
        ComputeNormalizationFactorsRecurse(forest.GetTree(t), 0, nTrainingPoints, Bounds(2), normalizationFactors[t]);
      }

      // Generate Visualization Image
      std::auto_ptr<Bitmap<PixelBgr> > result = std::auto_ptr<Bitmap<PixelBgr> >(
        new Bitmap<PixelBgr>(PlotSize.Width, PlotSize.Height) );

      // Paint the test data
      int index = 0;
      for (int j = 0; j < PlotSize.Height; j++)
      {
        for (int i = 0; i < PlotSize.Width; i++)
        {
          // Map pixel coordinate (i,j) in visualization image back to point in input space
          float x = plotCanvas.plotRangeX.first + i * plotCanvas.stepX;
          float y = plotCanvas.plotRangeY.first + j * plotCanvas.stepY;

          // Aggregate statistics for this sample over all trees
          double probability = 0.0;
          for (int t = 0; t < forest.TreeCount(); t++)
          {
            int leafIndex = leafNodeIndices[t][index];
            const GaussianPdf2d& pdf = forest.GetTree(t).GetNode(leafIndex).TrainingDataStatistics.GetPdf();
            probability += normalizationFactors[t][leafIndex] * pdf.GetProbability(x, y);
          }

          probability /= forest.TreeCount();

          // 'Gamma correct' probability density for better display
          float l = (float)(LuminanceScaleFactor* pow(probability,Gamma));

          if (l < 0)
            l = 0;
          else if (l > 255)
            l = 255;

          PixelBgr c = PixelBgr::FromArgb((unsigned char)(l), 0, 0);
          result->SetPixel(i, j, c);

          index++;
        }
      }

      // Also plot the original training data
      Graphics<PixelBgr> g(result->GetBuffer(), result->GetWidth(), result->GetHeight(), result->GetStride());

      for (unsigned int s = 0; s < trainingData.Count(); s++)
      {
        PointF x(
          (trainingData.GetDataPoint(s)[0] - plotCanvas.plotRangeX.first) / plotCanvas.stepX,
          (trainingData.GetDataPoint(s)[1] - plotCanvas.plotRangeY.first) / plotCanvas.stepY);

        RectangleF rectangle(x.X - 2.0f, x.Y - 2.0f, 4.0f, 4.0f);
        g.FillRectangle(DataPointColor, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        g.DrawRectangle(PixelBgr::FromArgb(0,0,0), rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
      }

      return result;
    }
  };
} } }

