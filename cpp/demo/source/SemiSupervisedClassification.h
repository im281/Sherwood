#pragma once

// This file defines types used to illustrate the use of the decision forest
// framework in a simple semi-supervised classification task (2D data points).

#include <iostream>

#include "Sherwood.h"

#include "FloydWarshall.h"
#include "Graphics.h"

#include "PlotCanvas.h"

#include "DataPointCollection.h"
#include "FeatureResponseFunctions.h"
#include "StatisticsAggregators.h"
#include "SemiSupervisedClassification.h"

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood 
{
  class SemiSupervisedClassificationTrainingContext : public ITrainingContext<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator>
  {
    // In semi-supervised training, we define information gain as a weighted
    // sum of supervised and unsupervised terms. This parameter describes the
    // importance of the unsupervised term relative to the supervised one.
    // For more information see:
    //  "A. Criminisi and J. Shotton, Decision Forests: for Computer Vision and
    //  Medical Image Analysis. Springer, 2013"
    static const double alpha_;

    int nClasses_;
    double a_, b_; // hyperparameters of prior used for density estimation

  public:
    SemiSupervisedClassificationTrainingContext(int nClasses, double a=10, double b=400)
    {
      a_ = a;
      b_ = b;
      nClasses_ = nClasses;
    }

    // Implementation of ITrainingContext
    LinearFeatureResponse2d GetRandomFeature(Random& random)
    {
      return LinearFeatureResponse2d((float)(2.0*random.NextDouble()-1.0), (float)(2.0*random.NextDouble()-1.0));
    }

    SemiSupervisedClassificationStatisticsAggregator GetStatisticsAggregator()
    {
      return SemiSupervisedClassificationStatisticsAggregator(nClasses_, a_, b_);
    }

    double ComputeInformationGain(const SemiSupervisedClassificationStatisticsAggregator& allStatistics, const SemiSupervisedClassificationStatisticsAggregator& leftStatistics, const SemiSupervisedClassificationStatisticsAggregator& rightStatistics)
    {
      double informationGainLabelled;
      {
        double entropyBefore = allStatistics.GetHistogramAggregator().Entropy();

        HistogramAggregator leftHistogram = leftStatistics.GetHistogramAggregator();
        HistogramAggregator rightHistogram = rightStatistics.GetHistogramAggregator();

        unsigned int nTotalSamples = leftHistogram.SampleCount() + rightHistogram.SampleCount();

        if (nTotalSamples <= 1)
        {
          informationGainLabelled = 0;
        }
        else
        {
          double entropyAfter = (leftHistogram.SampleCount() * leftHistogram.Entropy() + rightHistogram.SampleCount() * rightHistogram.Entropy()) / nTotalSamples;

          informationGainLabelled = entropyBefore - entropyAfter;
        }
      }

      double informationGainUnlabelled;
      {
        double entropyBefore = ((SemiSupervisedClassificationStatisticsAggregator)(allStatistics)).GetGaussianAggregator2d().GetPdf().Entropy();

        GaussianAggregator2d leftGaussian = leftStatistics.GetGaussianAggregator2d();
        GaussianAggregator2d rightGaussian = rightStatistics.GetGaussianAggregator2d();

        unsigned int nTotalSamples = leftGaussian.SampleCount() + rightGaussian.SampleCount();

        double entropyAfter = (leftGaussian.SampleCount() * leftGaussian.GetPdf().Entropy() + rightGaussian.SampleCount() * rightGaussian.GetPdf().Entropy()) / nTotalSamples;

        informationGainUnlabelled = entropyBefore - entropyAfter;
      }

      double gain =
        informationGainLabelled 
        + alpha_ * informationGainUnlabelled;

      return gain;
    }

    bool ShouldTerminate(const SemiSupervisedClassificationStatisticsAggregator& parent, const SemiSupervisedClassificationStatisticsAggregator& leftChild, const SemiSupervisedClassificationStatisticsAggregator& rightChild, double gain)
    {
      return gain < 0.4;
    }
  };

  class SemiSupervisedClassificationExample
  {
    static const PixelBgr UnlabelledDataPointColor;

  public:
    static std::auto_ptr<Forest<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator> > Train(
      const DataPointCollection& trainingData,
      const TrainingParameters& parameters,
      double a_,
      double b_)
    {
      // Train the forest
      std::cout << "Training the forest..." << std::endl;

      Random random;

      SemiSupervisedClassificationTrainingContext classificationContext(
        trainingData.CountClasses(), a_, b_);

      std::auto_ptr<Forest<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator> > forest
        = ForestTrainer<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator>::TrainForest(random, parameters, classificationContext, trainingData);

      // Label transduction to unlabelled leaves from nearest labelled leaf
      for (int t = 0; t < forest->TreeCount(); t++)
      {
        std::vector<int> unlabelledLeafIndices;
        std::vector<int> labelledLeafIndices;
        std::vector<int> closestLabelledLeafIndices;
        std::vector<int> leafIndices;

        Tree<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator>& tree = forest->GetTree(t);

        for (int n = 0; n < tree.NodeCount(); n++)
        {
          if (tree.GetNode(n).IsLeaf())
          {
            if (tree.GetNode(n).TrainingDataStatistics.GetHistogramAggregator().SampleCount() == 0)
              unlabelledLeafIndices.push_back(leafIndices.size());
            else
              labelledLeafIndices.push_back(leafIndices.size());

            leafIndices.push_back(n);
          }
        }

        // Build an upper triangular matrix of inter-leaf distances
        std::vector<float> interLeafDistances(leafIndices.size()*(leafIndices.size()+1)/2);
        unsigned int index = 0;
        for (std::vector<int>::size_type i = 0; i < leafIndices.size(); i++)
        {
          interLeafDistances[index++] = 0.0f; // skip diagonal
          for (std::vector<int>::size_type j = i + 1; j < leafIndices.size(); j++)
          {
            const SemiSupervisedClassificationStatisticsAggregator& a = tree.GetNode(leafIndices[i]).TrainingDataStatistics;
            const SemiSupervisedClassificationStatisticsAggregator& b = tree.GetNode(leafIndices[j]).TrainingDataStatistics;
            GaussianPdf2d x = a.GetGaussianAggregator2d().GetPdf();
            GaussianPdf2d y = b.GetGaussianAggregator2d().GetPdf();

            interLeafDistances[index++] = (float)(std::max(
              x.GetNegativeLogProbability((float)(y.MeanX()), (float)(y.MeanY())),
              y.GetNegativeLogProbability((float)(x.MeanX()), (float)(x.MeanY()))));
          }
        }
        assert(index==interLeafDistances.size());

        // Find shortest paths between all pairs of nodes in the graph of leaf nodes
        FloydWarshall pathFinder(&interLeafDistances[0], leafIndices.size());

        // Find the closest labelled leaf to each unlabelled leaf
        std::vector<float> minDistances(unlabelledLeafIndices.size());
        closestLabelledLeafIndices.resize(unlabelledLeafIndices.size());
        for (std::vector<float>::size_type i = 0; i < minDistances.size(); i++)
        {
          minDistances[i] = std::numeric_limits<float>::infinity();
          closestLabelledLeafIndices[i] = -1; // unused so deliberately invalid
        }

        for (std::vector<int>::size_type l = 0; l < labelledLeafIndices.size(); l++)
        {
          for (std::vector<int>::size_type u = 0; u < unlabelledLeafIndices.size(); u++)
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
        for (std::vector<int>::size_type u = 0; u < unlabelledLeafIndices.size(); u++)
        {
          Node<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator>& unlabelledLeaf
            = tree.GetNode(leafIndices[unlabelledLeafIndices[u]]);
          const Node<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator>& labelledLeaf
            = tree.GetNode(closestLabelledLeafIndices[u]);

          unlabelledLeaf.TrainingDataStatistics.GetHistogramAggregator()
            = labelledLeaf.TrainingDataStatistics.GetHistogramAggregator().DeepClone();
        }
      }

      return forest;
    }

    static std::auto_ptr<Bitmap<PixelBgr> > VisualizeLabels(const Forest<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator>& forest, DataPointCollection trainingData, Size PlotSize, PointF PlotDilation)
    {
      // Generate some test samples in a grid pattern (a useful basis for creating visualization images)
      PlotCanvas plotCanvas(trainingData.GetRange(0), trainingData.GetRange(1), PlotSize, PlotDilation);

      std::cout << "Applying the forest to test data..." << std::endl;

      std::auto_ptr<DataPointCollection> testData = std::auto_ptr<DataPointCollection>(
        DataPointCollection::Generate2dGrid(plotCanvas.plotRangeX, PlotSize.Width, plotCanvas.plotRangeY, PlotSize.Height) );

      std::vector<std::vector<int> > leafNodeIndices;
      forest.Apply(*testData.get(), leafNodeIndices);

      // Generate Visualization Image
      std::auto_ptr<Bitmap<PixelBgr> > result = std::auto_ptr<Bitmap<PixelBgr> >(
        new Bitmap<PixelBgr>(PlotSize.Width, PlotSize.Height) );

      // Paint the test data
      std::vector<std::vector<GaussianPdf2d> > leafDistributions(forest.TreeCount());
      for (int t = 0; t < forest.TreeCount(); t++)
      {
        leafDistributions[t].resize(forest.GetTree((t)).NodeCount());
        for (int i = 0; i < forest.GetTree((t)).NodeCount(); i++)
        {
          Node<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator> nodeCopy = forest.GetTree((t)).GetNode(i);

          if (nodeCopy.IsLeaf())
            leafDistributions[t][i] = nodeCopy.TrainingDataStatistics.GetGaussianAggregator2d().GetPdf();
        }
      }

      // Same colours as those in the book.
      assert(trainingData.CountClasses()<=4);
      PixelBgr colors[4];
      colors[0] = PixelBgr::FromArgb(183, 170, 8);
      colors[1] = PixelBgr::FromArgb(194, 32, 14);
      colors[2] = PixelBgr::FromArgb(4, 154, 10);
      colors[3] = PixelBgr::FromArgb(13, 26, 188);

      PixelBgr grey = PixelBgr::FromArgb(127, 127, 127);

      int index = 0;
      for (int j = 0; j < PlotSize.Height; j++)
      {
        for (int i = 0; i < PlotSize.Width; i++)
        {
          // Aggregate statistics for this sample over all leaf nodes reached
          HistogramAggregator h(trainingData.CountClasses());
          for (int t = 0; t < forest.TreeCount(); t++)
          {
            int leafIndex = leafNodeIndices[t][index];

            const SemiSupervisedClassificationStatisticsAggregator& a = forest.GetTree((t)).GetNode(leafIndex).TrainingDataStatistics;

            h.Aggregate(a.GetHistogramAggregator());
          }

          // Let's muddy the colors with a little grey where entropy is high.
          float mudiness = 0.5f*(float)(h.Entropy());

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

          PixelBgr c = PixelBgr::FromArgb((unsigned char)(R), (unsigned char)(G), (unsigned char)(B));

          result->SetPixel(i, j, c);

          index++;
        }
      }

      PaintTrainingData(trainingData, plotCanvas, result.get());

      return result;
    }

    static std::auto_ptr<Bitmap<PixelBgr> > VisualizeDensity(const Forest<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator>& forest, DataPointCollection trainingData, Size PlotSize, PointF PlotDilation)
    {
      // Generate some test samples in a grid pattern (a useful basis for creating visualization images)
      PlotCanvas plotCanvas(trainingData.GetRange(0), trainingData.GetRange(1), PlotSize, PlotDilation);

      std::cout << "\nApplying the forest to test data..." << std::endl;

      std::auto_ptr<DataPointCollection> testData = std::auto_ptr<DataPointCollection>(
        DataPointCollection::Generate2dGrid(plotCanvas.plotRangeX, PlotSize.Width, plotCanvas.plotRangeY, PlotSize.Height) );

      std::vector<std::vector<int> > leafNodeIndices;
      forest.Apply(*testData.get(), leafNodeIndices);

      std::auto_ptr<Bitmap<PixelBgr> > result = std::auto_ptr<Bitmap<PixelBgr> >(
        new Bitmap<PixelBgr>(PlotSize.Width, PlotSize.Height) );

      // Paint the test data
      std::vector<std::vector<GaussianPdf2d> > leafDistributions(forest.TreeCount());
      for (int t = 0; t < forest.TreeCount(); t++)
      {
        leafDistributions[t].resize(forest.GetTree((t)).NodeCount());
        for (int i = 0; i < forest.GetTree((t)).NodeCount(); i++)
        {
          Node<LinearFeatureResponse2d, SemiSupervisedClassificationStatisticsAggregator> nodeCopy = forest.GetTree((t)).GetNode(i);

          if (nodeCopy.IsLeaf())
            leafDistributions[t][i] = nodeCopy.TrainingDataStatistics.GetGaussianAggregator2d().GetPdf();
        }
      }
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
            probability += leafDistributions[t][leafIndex].GetProbability(x, y);
          }

          probability /= forest.TreeCount();

          float l = 2000000.0f * (float)probability;

          if (l < 0)
            l = 0;
          else if (l > 255)
            l = 255;

          PixelBgr c = PixelBgr::FromArgb((unsigned char)(l), 0, 0);
          result->SetPixel(i, j, c);

          index++;
        }
      }

      PaintTrainingData(trainingData, plotCanvas, result.get());

      return result;
    }

    static void PaintTrainingData(DataPointCollection trainingData, PlotCanvas plotCanvas, Bitmap<PixelBgr>* result)
    {
      // same colours as those in the book.
      assert(trainingData.CountClasses()<=4);
      PixelBgr colors[4];
      colors[0] = PixelBgr::FromArgb(183, 170, 8);
      colors[1] = PixelBgr::FromArgb(194, 32, 14);
      colors[2] = PixelBgr::FromArgb(4, 154, 10);
      colors[3] = PixelBgr::FromArgb(13, 26, 188);

      // Also plot the original training data (a little bigger for clarity)
      Graphics<PixelBgr> g(result->GetBuffer(), result->GetWidth(), result->GetHeight(), result->GetStride());

      {
        // Paint unlabelled data
        for (unsigned int s = 0; s < trainingData.Count(); s++)
        {
          if (trainingData.GetIntegerLabel(s) == DataPointCollection::UnknownClassLabel)
          {
            PointF x(
              (trainingData.GetDataPoint(s)[0] - plotCanvas.plotRangeX.first) / plotCanvas.stepX,
              (trainingData.GetDataPoint(s)[1] - plotCanvas.plotRangeY.first) / plotCanvas.stepY);

            RectangleF rectangle(x.X - 2.0f, x.Y - 2.0f, 4.0f, 4.0f);
            g.FillRectangle(UnlabelledDataPointColor, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            g.DrawRectangle(PixelBgr::FromArgb(0,0,0), rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
          }
        }

        // Paint labelled data on top
        for (unsigned int s = 0; s < trainingData.Count(); s++)
        {
          if (trainingData.GetIntegerLabel(s) != DataPointCollection::UnknownClassLabel)
          {
            PointF x(
              (trainingData.GetDataPoint(s)[0] - plotCanvas.plotRangeX.first) / plotCanvas.stepX,
              (trainingData.GetDataPoint(s)[1] - plotCanvas.plotRangeY.first) / plotCanvas.stepY);

            RectangleF rectangle(x.X - 5.0f, x.Y - 5.0f, 10.0f, 10.0f);
            g.FillRectangle(colors[trainingData.GetIntegerLabel(s)], rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            g.DrawRectangle(PixelBgr::FromArgb(255,255,255), rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
          }
        }
      }
    }
  };
} } }
