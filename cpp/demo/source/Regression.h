#pragma once

// This file defines types used to illustrate the use of the decision forest
// library in simple 1D to 1D regression task.

#include <string>
#include <vector>
#include <set>
#include <map>
#include <iostream>
#include <algorithm>
#include <cctype>
#include <sstream>

#include "PlotCanvas.h"
#include "Graphics.h"

#include "Sherwood.h"

#include "DataPointCollection.h"
#include "FeatureResponseFunctions.h"
#include "StatisticsAggregators.h"
#include "Regression.h"

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  class RegressionTrainingContext : public ITrainingContext<AxisAlignedFeatureResponse, LinearFitAggregator1d>
  {
  public:
    // Implementation of ITrainingContext
    AxisAlignedFeatureResponse GetRandomFeature(Random& random)
    {
      return AxisAlignedFeatureResponse(0); // not actually random because only one feature possible  in 1D
    }

    LinearFitAggregator1d GetStatisticsAggregator()
    {
      return LinearFitAggregator1d();
    }

    double ComputeInformationGain(const LinearFitAggregator1d& allStatistics, const LinearFitAggregator1d& leftStatistics, const LinearFitAggregator1d& rightStatistics)
    {
      double entropyBefore = ((LinearFitAggregator1d)(allStatistics)).Entropy();

      LinearFitAggregator1d leftLineFitStats = (LinearFitAggregator1d)(leftStatistics);
      LinearFitAggregator1d rightLineFitStatistics = (LinearFitAggregator1d)(rightStatistics);

      unsigned int nTotalSamples = leftLineFitStats.SampleCount() + rightLineFitStatistics.SampleCount();

      double entropyAfter = (leftLineFitStats.SampleCount() * leftLineFitStats.Entropy() + rightLineFitStatistics.SampleCount() * rightLineFitStatistics.Entropy()) / nTotalSamples;

      return entropyBefore - entropyAfter;
    }

    bool ShouldTerminate(const LinearFitAggregator1d& parent, const LinearFitAggregator1d& leftChild, const LinearFitAggregator1d& rightChild, double gain)
    {
      return gain < 0.05;
    }
  };

  class RegressionExample
  {
  public:
    static const PixelBgr DensityColor;
    static const PixelBgr DataPointColor;
    static const PixelBgr DataPointBorderColor;
    static const PixelBgr MeanColor;

    static std::auto_ptr<Forest<AxisAlignedFeatureResponse, LinearFitAggregator1d> > Train(
      const DataPointCollection& trainingData,
      const TrainingParameters& parameters)
    {
      std::cout << "Training the forest..." << std::endl;

      Random random;

      RegressionTrainingContext regressionTrainingContext;

      std::auto_ptr<Forest<AxisAlignedFeatureResponse, LinearFitAggregator1d> > forest
        = ForestTrainer<AxisAlignedFeatureResponse, LinearFitAggregator1d>::TrainForest(
        random, parameters, regressionTrainingContext, trainingData);

      return forest;
    }

    static std::auto_ptr<Bitmap<PixelBgr> > Visualize(
      Forest<AxisAlignedFeatureResponse, LinearFitAggregator1d>& forest,
      const DataPointCollection& trainingData,
      Size PlotSize,
      PointF PlotDilation )
    {
      // Generate some test samples in a grid pattern (a useful basis for creating visualization images)
      PlotCanvas plotCanvas(trainingData.GetRange(0), trainingData.GetTargetRange(), PlotSize, PlotDilation);

      std::auto_ptr<DataPointCollection> testData = DataPointCollection::Generate1dGrid(plotCanvas.plotRangeX, PlotSize.Width);

      std::cout << "\nApplying the forest to test data..." << std::endl;

      std::vector<std::vector<int> > leafNodeIndices;
      forest.Apply(*testData.get(), leafNodeIndices);

      // Generate visualization image
      std::auto_ptr<Bitmap<PixelBgr> > result = std::auto_ptr<Bitmap<PixelBgr> >(new Bitmap<PixelBgr> (PlotSize.Width, PlotSize.Height));

      // Plot the learned density
      PixelBgr inverseDensityColor = PixelBgr::FromArgb(255 - DensityColor.R, 255 - DensityColor.G, 255 - DensityColor.B);

      std::vector<double> mean_y_given_x(PlotSize.Width);

      int index = 0;
      for (int i = 0; i < PlotSize.Width; i++)
      {
        double totalProbability = 0.0;
        for (int j = 0; j < PlotSize.Height; j++)
        {
          // Map pixel coordinate (i,j) in visualization image back to point in input space
          float x = plotCanvas.plotRangeX.first + i * plotCanvas.stepX;
          float y = plotCanvas.plotRangeY.first + j * plotCanvas.stepY;

          double probability = 0.0;

          // Aggregate statistics for this sample over all trees
          for (int t = 0; t < forest.TreeCount(); t++)
          {
            Node<AxisAlignedFeatureResponse, LinearFitAggregator1d> leafNodeCopy = forest.GetTree((t)).GetNode(leafNodeIndices[t][i]);

            const LinearFitAggregator1d& leafStatistics = leafNodeCopy.TrainingDataStatistics;

            probability += leafStatistics.GetProbability(x, y);
          }

          probability /= forest.TreeCount();

          mean_y_given_x[i] += probability * y;
          totalProbability += probability;

          float scale = 10.0f * (float)probability;

          PixelBgr weightedColor = PixelBgr::FromArgb(
            (unsigned char)(std::min(scale * inverseDensityColor.R + 0.5f, 255.0f)),
            (unsigned char)(std::min(scale * inverseDensityColor.G + 0.5f, 255.0f)),
            (unsigned char)(std::min(scale * inverseDensityColor.B + 0.5f, 255.0f)));

          PixelBgr c = PixelBgr::FromArgb(255 - weightedColor.R, 255 - weightedColor.G, 255 - weightedColor.G);

          result->SetPixel(i, j, c);

          index++;
        }

        // NB We don't really compute the mean over y, just over the region of y that is plotted
        mean_y_given_x[i] /= totalProbability;
      }

      // Also plot the mean curve and the original training data
      Graphics<PixelBgr> g(result->GetBuffer(), result->GetWidth(), result->GetHeight(), result->GetStride());

      for (int i = 0; i < PlotSize.Width-1; i++)
      {
        g.DrawLine(
          MeanColor,
          (float)(i),
          (float)((mean_y_given_x[i] - plotCanvas.plotRangeY.first)/plotCanvas.stepY),
          (float)(i+1),
          (float)((mean_y_given_x[i+1] - plotCanvas.plotRangeY.first)/plotCanvas.stepY));
      }

      for (unsigned int s = 0; s < trainingData.Count(); s++)
      {
        // Map sample coordinate back to a pixel coordinate in the visualization image
        PointF x(
          (trainingData.GetDataPoint(s)[0] - plotCanvas.plotRangeX.first) / plotCanvas.stepX,
          (trainingData.GetTarget(s) - plotCanvas.plotRangeY.first) / plotCanvas.stepY);

        RectangleF rectangle(x.X - 2.0f, x.Y - 2.0f, 4.0f, 4.0f);
        g.FillRectangle(DataPointColor, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        g.DrawRectangle(DataPointBorderColor, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
      }

      return result;
    }
  };
} } }
