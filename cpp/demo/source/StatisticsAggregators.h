#pragma once

// This file defines some IStatisticsAggregator implementations used by the
// example code in Classification.h, DensityEstimation.h, etc. Note we
// represent IStatisticsAggregator instances using simple structs so that all
// tree data can be stored contiguously in a linear array.

#include <math.h>

#include <limits>
#include <vector>

#include "Sherwood.h"

#include "DataPointCollection.h"

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  struct HistogramAggregator
  {
  private:
    unsigned short bins_[4];
    int binCount_;

    unsigned int sampleCount_;

  public:
    double Entropy() const;

    HistogramAggregator();

    HistogramAggregator(int nClasses);

    float GetProbability(int classIndex) const;

    int BinCount() const {return binCount_; }

    unsigned int SampleCount() const { return sampleCount_; }

    int FindTallestBinIndex() const;

    // IStatisticsAggregator implementation
    void Clear();

    void Aggregate(const IDataPointCollection& data, unsigned int index);

    void Aggregate(const HistogramAggregator& aggregator);

    HistogramAggregator DeepClone() const;
  };

  class GaussianPdf2d
  {
  private:
    double mean_x_, mean_y_;
    double Sigma_11_, Sigma_12_, Sigma_22_; // symmetric 2x2 covariance matrix
    double inv_Sigma_11_, inv_Sigma_12_, inv_Sigma_22_; // symmetric 2x2 inverse covariance matrix
    double det_Sigma_;
    double log_det_Sigma_;

  public:
    GaussianPdf2d() { }

    GaussianPdf2d(double mu_x, double mu_y, double Sigma_11, double Sigma_12, double Sigma_22);

    double MeanX() const
    {
      return mean_x_;
    }

    double MeanY() const
    {
      return mean_y_;
    }

    double VarianceX() const
    {
      return Sigma_11_;
    }

    double VarianceY() const
    {
      return Sigma_22_;
    }

    double CovarianceXY() const
    {
      return Sigma_12_;
    }

    double GetProbability(float x, float y) const;

    double GetNegativeLogProbability(float x, float y) const;

    double Entropy() const;
  };

  struct GaussianAggregator2d
  {
  private:
    unsigned int sampleCount_;

    double sx_, sy_;    // sum
    double sxx_, syy_;  // sum squares
    double sxy_;        // sum products

    double a_, b_;      // hyperparameters of prior

  public:
    GaussianAggregator2d()
    {
      Clear();
    }

    GaussianAggregator2d(double a, double b);

    GaussianPdf2d GetPdf() const;

    unsigned int SampleCount() const {  return sampleCount_; }

    // IStatisticsAggregator implementation
    void Clear();

    void Aggregate(const IDataPointCollection& data, unsigned int index);

    void Aggregate(const GaussianAggregator2d& aggregator);

    GaussianAggregator2d DeepClone() const;
  };

  struct SemiSupervisedClassificationStatisticsAggregator
  {
    int nClasses_;
    double a_, b_;

    GaussianAggregator2d gaussianAggregator2d_;
    HistogramAggregator histogramAggregator_;

  public:
    SemiSupervisedClassificationStatisticsAggregator()
    {

    }

    GaussianAggregator2d& GetGaussianAggregator2d() { return gaussianAggregator2d_; }
    HistogramAggregator& GetHistogramAggregator() { return histogramAggregator_; }

    const GaussianAggregator2d& GetGaussianAggregator2d() const { return gaussianAggregator2d_; }
    const HistogramAggregator& GetHistogramAggregator() const { return histogramAggregator_; }

    SemiSupervisedClassificationStatisticsAggregator(int nClasses, double a, double b);

    // IStatisticsAggregator implementation
    void Clear();

    void Aggregate(const IDataPointCollection& data, unsigned int index);

    void Aggregate(const SemiSupervisedClassificationStatisticsAggregator& aggregator);

    SemiSupervisedClassificationStatisticsAggregator DeepClone() const;
  };

  struct LinearFitAggregator1d
  {
  private:
    static const double Pi, E;

    unsigned int sampleCount_;

    // Good reference for Bayesian linear regression: http://see.stanford.edu/materials/aimlcs229/cs229-gp.pdf

    double XT_X_11_, XT_X_12_;
    double XT_X_21_, XT_X_22_;

    double XT_Y_1_;
    double XT_Y_2_;

    double Y2_;

  public:
    double Entropy()
    {
      if (sampleCount_ < 3)
        return std::numeric_limits<double>::infinity();

      double determinant = XT_X_11_ * XT_X_22_ - XT_X_12_ * XT_X_12_;

      if (determinant == 0.0)
        return std::numeric_limits<double>::infinity();

      return 0.5 * log(pow(2.0 * Pi * E, 2.0) * determinant);
    }

    LinearFitAggregator1d()
    {
    }

    double GetProbability(float x, float y) const
    {
      // http://mathworld.wolfram.com/CorrelationCoefficient.html
      double mean_x = XT_X_12_ / sampleCount_;
      double ss_x = XT_X_11_ - sampleCount_ * mean_x * mean_x;

      double mean_y = XT_Y_2_ / sampleCount_;
      double ss_y = Y2_ - sampleCount_ * mean_y * mean_y;

      double ss_xy = XT_Y_1_ - sampleCount_ * mean_y * mean_x;

      double r2 = (ss_xy * ss_xy) / (ss_x * ss_y);
      double sigma_2 = ss_y * (1.0 - r2) / sampleCount_;

      // http://see.stanford.edu/materials/aimlcs229/cs229-gp.pdf

      double determinant = XT_X_11_ * XT_X_22_ - XT_X_12_ * XT_X_12_;

      double A_11 = sigma_2 * XT_X_22_ / determinant, A_12 = -sigma_2 * XT_X_12_ / determinant;
      double A_21 = -sigma_2 * XT_X_12_ / determinant, A_22 = sigma_2 * XT_X_11_ / determinant;

      double mean = (x * (A_11 * XT_Y_1_ + A_12 * XT_Y_2_)) / (sigma_2) + (A_21 * XT_Y_1_ + A_22 * XT_Y_2_) / (sigma_2);
      double variance = x * (A_11 * x + A_12) + (A_21 * x + A_22) + sigma_2;

      return pow(2.0 * Pi, -0.5) * pow(variance, -0.5) * exp(-0.5 * (y - mean) * (y - mean) / (variance));
    }

    unsigned int SampleCount() const
    {
      return sampleCount_;
    }

    // IStatisticsAggregator implementation
    void Clear()
    {
      XT_X_11_ = 0.0; XT_X_12_ = 0.0;
      XT_X_21_ = 0.0; XT_X_22_ = 0.0;

      XT_Y_1_ = 0.0;
      XT_Y_2_ = 0.0;

      Y2_ = 0.0;

      sampleCount_ = 0;
    }

    void Aggregate(const IDataPointCollection& data, unsigned int index)
    {
      const DataPointCollection& concreteData = (const DataPointCollection&)(data);

      const float* datum = concreteData.GetDataPoint((int)index);
      float target = concreteData.GetTarget((int)index);

      XT_X_11_ += datum[0] * datum[0];
      XT_X_12_ += datum[0];
      XT_X_21_ += datum[0];
      XT_X_22_ += 1.0;

      XT_Y_1_ += datum[0] * target;
      XT_Y_2_ += target;

      Y2_ += target * target;

      sampleCount_ += 1;
    }

    void Aggregate(const LinearFitAggregator1d& aggregator)
    {
      LinearFitAggregator1d linearFitAggregator = (LinearFitAggregator1d)(aggregator);

      XT_X_11_ += linearFitAggregator.XT_X_11_; XT_X_12_ += linearFitAggregator.XT_X_12_;
      XT_X_21_ += linearFitAggregator.XT_X_21_; XT_X_22_ += linearFitAggregator.XT_X_22_;

      XT_Y_1_ += linearFitAggregator.XT_Y_1_;
      XT_Y_2_ += linearFitAggregator.XT_Y_2_;

      Y2_ += linearFitAggregator.Y2_;

      sampleCount_ += linearFitAggregator.sampleCount_;
    }

    LinearFitAggregator1d DeepClone() const
    {
      LinearFitAggregator1d result;

      result.XT_X_11_ = XT_X_11_; result.XT_X_12_ = XT_X_12_;
      result.XT_X_21_ = XT_X_21_; result.XT_X_22_ = XT_X_22_;

      result.XT_Y_1_ = XT_Y_1_;
      result.XT_Y_2_ = XT_Y_2_;

      result.Y2_ = Y2_;

      result.sampleCount_ = sampleCount_;

      return result;
    }
  };
} } }
