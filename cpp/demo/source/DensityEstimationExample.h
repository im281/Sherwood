#pragma once

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
#include "Features.h"
#include "StatisticsAggregators.h"
#include "DensityEstimation.h"
#include "PlotCanvas.h"

// TODO: This four level namespace not the same as C#
namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
	class DensityEstimationExample
    {
	public:
         static const Color DataPointColor;

        // TODO: Need to build a test harness around this bounds formation code
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

            Bounds Clone()
            {
				// TODO: Looks silly
                Bounds b(Lower.size());
                b.Lower = Lower;
                b.Upper = Upper;

                return b;
            }

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
			const Tree<AxisAlignedFeature, GaussianAggregator2d>& t,
			int nodeIndex, Bounds bounds,
			std::vector<double>& normalizationFactors)
        {
            Node<AxisAlignedFeature, GaussianAggregator2d> nodeCopy = t.GetNode(nodeIndex);

            // Evaluate integral of bivariate normal distribution within this nodes bounds
            GaussianAggregator2d aggregator = nodeCopy.TrainingDataStatistics;

            GaussianPdf2d g = aggregator.GetPdf();

            double u = CumulativeNormalDistribution2d::M(
                (bounds.Upper[0] - g.MeanX()) / sqrt(g.VarianceX()),
                (bounds.Upper[1] - g.MeanY()) / sqrt(g.VarianceY()),
                g.CovarianceXY() / sqrt(g.VarianceX() * g.VarianceY()));

            double l = CumulativeNormalDistribution2d::M(
                (bounds.Lower[0] - g.MeanX()) / sqrt(g.VarianceX()),
                (bounds.Lower[1] - g.MeanY()) / sqrt(g.VarianceY()),
                g.CovarianceXY() / sqrt(g.VarianceX() * g.VarianceY()));

            normalizationFactors[nodeIndex] = u - l;

            if (!nodeCopy.IsLeaf())
            {
                AxisAlignedFeature feature = (AxisAlignedFeature)(nodeCopy.Feature);

                Bounds leftChildBounds = bounds.Clone();
                leftChildBounds.Upper[feature.Axis()] = nodeCopy.Threshold;
                ComputeNormalizationFactorsRecurse(t, nodeIndex * 2 + 1, leftChildBounds, normalizationFactors);

                Bounds rightChildBounds = bounds.Clone();
                rightChildBounds.Lower[feature.Axis()] = nodeCopy.Threshold;
                ComputeNormalizationFactorsRecurse(t, nodeIndex * 2 + 2, rightChildBounds, normalizationFactors);
            }
        }

        static std::auto_ptr<Forest<AxisAlignedFeature, GaussianAggregator2d> > Train(
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

            // Train the forest
            std::cout << "Training the forest..." << std::endl;

            Random random;
            ForestTrainer<AxisAlignedFeature, GaussianAggregator2d> forestTrainer(random);

			DensityEstimationTrainingContext densityEstimationTrainingContext(a, b, random);
            std::auto_ptr<Forest<AxisAlignedFeature, GaussianAggregator2d> > forest = forestTrainer.TrainForest(
				parameters,
				densityEstimationTrainingContext,
				trainingData );

            return forest;
        }

        static std::auto_ptr<Bitmap<Color> > Visualize(
            Forest<AxisAlignedFeature, GaussianAggregator2d>& forest,
            DataPointCollection& trainingData,
            Size PlotSize,
            PointF PlotDilation) 
        {

            // Generate some test samples in a grid pattern (a useful basis for creating visualization images)
            PlotCanvas plotCanvas(trainingData.GetRange(0), trainingData.GetRange(1), PlotSize, PlotDilation);

            // Apply the trained forest to the test data
            std::cout << "\nApplying the forest to test data..." << std::endl;

            std::auto_ptr<DataPointCollection> testData = std::auto_ptr<DataPointCollection>(
				DataPointCollection::Generate2dGrid(plotCanvas.plotRangeX, PlotSize.Width, plotCanvas.plotRangeY, PlotSize.Height) );

            //System.Diagnostics.Stopwatch w = new System.Diagnostics.Stopwatch();
            //w.Start();
            std::vector<std::vector<int> > leafNodeIndices;
			forest.Apply(*(testData.get()), leafNodeIndices);
            //w.Stop();
            //Console.WriteLine("Evaluation time was {0} ms", w.ElapsedMilliseconds);

            // Compute normalization factors per node
			std::vector<std::vector<double> > normalizationFactors(forest.TreeCount());
            for (int t = 0; t < forest.TreeCount(); t++)
            {
                normalizationFactors[t].resize(forest.GetTree(t)->NodeCount());
                ComputeNormalizationFactorsRecurse(*(forest.GetTree(t)), 0, Bounds(2), normalizationFactors[t]);
            }

            // Generate Visualization Image
            std::auto_ptr<Bitmap<Color> > result = std::auto_ptr<Bitmap<Color> >(
				new Bitmap<Color>(PlotSize.Width, PlotSize.Height) );

            // Paint the test data
            std::vector<std::vector<GaussianPdf2d> > leafDistributions(forest.TreeCount());
            for (int t = 0; t < forest.TreeCount(); t++)
            {
                leafDistributions[t].resize(forest.GetTree(t)->NodeCount());
                for (int i = 0; i < forest.GetTree(t)->NodeCount(); i++)
                {
                    Node<AxisAlignedFeature, GaussianAggregator2d> nodeCopy = forest.GetTree(t)->GetNode(i);
                    if (/*node!=null &&*/ nodeCopy.IsLeaf())
                    {
                        // System.Diagnostics.Debug.Assert(node.TrainingDataStatistics != null);
                        leafDistributions[t][i] = nodeCopy.TrainingDataStatistics.GetPdf();
                    }
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

                        double f = 1.0 / normalizationFactors[t][leafIndex];

                        probability += f * leafDistributions[t][leafIndex].GetProbability(x, y);
                    }

                    probability /= forest.TreeCount();

                    // TODO: Avoid hard coded parameter choices

                    // 'Gamma correct' probability density for better display
                    float l = 2500.0f * (float)(pow(probability,0.333));

                    if (l < 0)
                        l = 0;
                    else if (l > 255)
                        l = 255;

                    Color c = Color::FromArgb((unsigned char)(l), 0, 0);
                    result->SetPixel(i, j, c);

                    index++;
                }
            }
			
            // Also plot the original training data
			Graphics<Color> g(result->GetBuffer(), result->GetWidth(), result->GetHeight(), result->GetStride());

            for (unsigned int s = 0; s < trainingData.Count(); s++)
            {
                PointF x(
                    (trainingData.GetDataPoint(s)[0] - plotCanvas.plotRangeX.first) / plotCanvas.stepX,
                    (trainingData.GetDataPoint(s)[1] - plotCanvas.plotRangeY.first) / plotCanvas.stepY);

                RectangleF rectangle(x.X - 2.0f, x.Y - 2.0f, 4.0f, 4.0f);
                g.FillRectangle(DataPointColor, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
				g.DrawRectangle(Color::FromArgb(0,0,0), rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            }

            return result;
        }
    };
} } }

