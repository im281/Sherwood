#include "StatisticsAggregators.h"

#include <iostream>

#include "DataPointCollection.h"

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  double HistogramAggregator::Entropy() const
  {
    if (sampleCount_ == 0)
      return 0.0;

    double result = 0.0;
    for (int b = 0; b < BinCount(); b++)
    {
      double p = (double)bins_[b] / (double)sampleCount_;
      result -= p == 0.0 ? 0.0 : p * log(p)/log(2.0);
    }

    return result;
  }

  HistogramAggregator::HistogramAggregator()
  {
    binCount_ = 0;
    for(int b=0; b<binCount_; b++)
      bins_[b] = 0;
    sampleCount_ = 0;
  }

  HistogramAggregator::HistogramAggregator(int nClasses)
  {
    if(nClasses>4)
      throw std::runtime_error("HistogramAggregator supports a maximum of four classes.");
    binCount_ = nClasses;
    for(int b=0; b<binCount_; b++)
      bins_[b] = 0;
    sampleCount_ = 0;
  }

  float HistogramAggregator::GetProbability(int classIndex) const
  {
    return (float)(bins_[classIndex]) / sampleCount_;
  }

  int HistogramAggregator::FindTallestBinIndex() const
  {
    unsigned int maxCount = bins_[0];
    int tallestBinIndex = 0;

    for (int i = 1; i < BinCount(); i++)
    {
      if (bins_[i] > maxCount)
      {
        maxCount = bins_[i];
        tallestBinIndex = i;
      }
    }

    return tallestBinIndex;
  }

  // IStatisticsAggregator implementation
  void HistogramAggregator::Clear()
  {
    for (int b = 0; b < BinCount(); b++)
      bins_[b] = 0;

    sampleCount_ = 0;
  }

  void HistogramAggregator::Aggregate(const IDataPointCollection& data, unsigned int index)
  {
    const DataPointCollection& concreteData = (const DataPointCollection&)(data);

    bins_[concreteData.GetIntegerLabel((int)index)]++;
    sampleCount_ += 1;
  }

  void HistogramAggregator::Aggregate(const HistogramAggregator& aggregator)
  {
    assert(aggregator.BinCount() == BinCount());

    for (int b = 0; b < BinCount(); b++)
      bins_[b] += aggregator.bins_[b];

    sampleCount_ += aggregator.sampleCount_;
  }

  HistogramAggregator HistogramAggregator::DeepClone() const
  {
    HistogramAggregator result(BinCount());

    for (int b = 0; b < BinCount(); b++)
      result.bins_[b] = bins_[b];

    result.sampleCount_ = sampleCount_;

    return result;
  }

  GaussianPdf2d::GaussianPdf2d(double mu_x, double mu_y, double Sigma_11, double Sigma_12, double Sigma_22)
  {
    mean_x_ = mu_x;
    mean_y_ = mu_y;

    Sigma_11_ = Sigma_11;
    Sigma_12_ = Sigma_12;
    Sigma_22_ = Sigma_22;

    det_Sigma_ = Sigma_11 * Sigma_22 - Sigma_12 * Sigma_12;

    if (det_Sigma_ < 0.0)
      throw std::runtime_error("Gaussian covaraince matrix must have determinant>0.0.");

    log_det_Sigma_ = log(det_Sigma_);

    inv_Sigma_11_ = Sigma_22 / det_Sigma_;
    inv_Sigma_22_ = Sigma_11 / det_Sigma_;
    inv_Sigma_12_ = -Sigma_12 / det_Sigma_;
  }

  double GaussianPdf2d::GetProbability(float x, float y) const
  {
    double x_ = x - mean_x_;
    double y_ = y - mean_y_;

    return pow(2.0 * 3.141593, -1.0) * pow(det_Sigma_, -0.5) * exp(
      -0.5 * x_ * (inv_Sigma_11_ * x_ + inv_Sigma_12_ * y_) - 0.5 * y_ * (inv_Sigma_12_ * x_ + inv_Sigma_22_ * y_));
  }

  double GaussianPdf2d::GetNegativeLogProbability(float x, float y) const
  {
    double x_ = x - mean_x_;
    double y_ = y - mean_y_;

    double result = 0.5 * log_det_Sigma_ + 0.5 * (x_ * (inv_Sigma_11_ * x_ + inv_Sigma_12_ * y_) + y_ * (inv_Sigma_12_ * x_ + inv_Sigma_22_ * y_));

    return result;
  }

  double GaussianPdf2d::Entropy() const
  {
    double determinant = Sigma_11_ * Sigma_22_ - Sigma_12_ * Sigma_12_;

    if (determinant <= 0.0)
    {
      // If we used a sensible prior, this wouldn't happen. So the user can test
      // without a prior, we fail gracefully.
      return std::numeric_limits<double>::infinity();
    }

    return 0.5 * log(pow(2.0 * 3.141593 * 2.718282, 2.0) * determinant);

  }

  GaussianAggregator2d::GaussianAggregator2d(double a, double b)
  {
    assert(a >= 0.0 && b >= 0.0); // Hyperparameters a and b must be greater than or equal to zero.

    sx_ = 0.0; sy_ = 0.0;
    sxx_ = 0.0; syy_ = 0.0;
    sxy_ = 0.0;
    sampleCount_ = 0;

    a_ = a;
    b_ = b;

    // The prior should guarantee non-degeneracy but the caller can
    // deactivate it (by setting hyperparameter a to 0.0). In this event
    // we have to tweak things slightly to ensure non-degenerate covariance matrices.
    if (a_ < 0.001)
      a_ = 0.001;
    if (b_ < 1)
      b_ = 1.0;
  }

  GaussianPdf2d GaussianAggregator2d::GetPdf() const
  {
    // Compute maximum likelihood mean and covariance matrix
    double mx = sx_ / sampleCount_;
    double my = sy_ / sampleCount_;
    double vxx = sxx_ / sampleCount_ - (sx_ * sx_) / (sampleCount_ * sampleCount_);
    double vyy = syy_ / sampleCount_ - (sy_ * sy_) / (sampleCount_ * sampleCount_);
    double vxy = sxy_ / sampleCount_ - (sx_ * sy_) / (sampleCount_ * sampleCount_);

    // Adapt using conjugate prior
    double alpha = sampleCount_/(sampleCount_ + a_);
    vxx = alpha * vxx + (1 - alpha) * b_;
    vyy = alpha * vyy + (1 - alpha) * b_;
    vxy = alpha * vxy;

    return GaussianPdf2d(mx, my, vxx, vxy, vyy);
  }

  // IStatisticsAggregator implementation
  void GaussianAggregator2d::Clear()
  {
    sx_ = 0.0; sy_ = 0.0;
    sxx_ = 0.0; syy_ = 0.0;
    sxy_ = 0.0;
    sampleCount_ = 0;
  }

  void GaussianAggregator2d::Aggregate(const IDataPointCollection& data, unsigned int index)
  {
    const DataPointCollection& concreteData = (const DataPointCollection&)(data);

    sx_ += concreteData.GetDataPoint((int)index)[0];
    sy_ += concreteData.GetDataPoint((int)index)[1];

    sxx_ += pow((double)(concreteData.GetDataPoint((int)index)[0]), 2.0);
    syy_ += pow((double)(concreteData.GetDataPoint((int)index)[1]), 2.0);

    sxy_ += concreteData.GetDataPoint((int)index)[0] * concreteData.GetDataPoint((int)index)[1];

    sampleCount_ += 1;
  }

  void GaussianAggregator2d::Aggregate(const GaussianAggregator2d& aggregator)
  {
    sx_ += aggregator.sx_;
    sy_ += aggregator.sy_;

    sxx_ += aggregator.sxx_;
    syy_ += aggregator.syy_;

    sxy_ += aggregator.sxy_;

    sampleCount_ += aggregator.sampleCount_;
  }

  GaussianAggregator2d GaussianAggregator2d::DeepClone() const
  {
    GaussianAggregator2d result(a_, b_); 

    result.sx_ = sx_;
    result.sy_ = sy_;

    result.sxx_ = sxx_;
    result.syy_ = syy_;

    result.sxy_ = sxy_;

    result.sampleCount_ = sampleCount_;

    result.a_ = a_;
    result.b_ = b_;

    return result;
  }


  SemiSupervisedClassificationStatisticsAggregator::SemiSupervisedClassificationStatisticsAggregator(int nClasses, double a, double b):
  gaussianAggregator2d_(a, b),
    histogramAggregator_(nClasses)
  {
    nClasses_ = nClasses;
    a_ = a;
    b_ = b;
  }

  // IStatisticsAggregator implementation
  void SemiSupervisedClassificationStatisticsAggregator::Clear()
  {
    gaussianAggregator2d_.Clear();
    histogramAggregator_.Clear();
  }

  void SemiSupervisedClassificationStatisticsAggregator::Aggregate(const IDataPointCollection& data, unsigned int index)
  {
    const DataPointCollection& concreteData = (const DataPointCollection&)(data);

    // Always aggregate density statistics
    gaussianAggregator2d_.Aggregate(data, index);

    // Only aggregate histogram statistics for those data points that have class labels
    if (concreteData.GetIntegerLabel((int)(index)) != DataPointCollection::UnknownClassLabel)
      histogramAggregator_.Aggregate(data, index);
  }

  void SemiSupervisedClassificationStatisticsAggregator::Aggregate(const SemiSupervisedClassificationStatisticsAggregator& aggregator)
  {
    gaussianAggregator2d_.Aggregate(aggregator.gaussianAggregator2d_);
    histogramAggregator_.Aggregate(aggregator.histogramAggregator_);
  }

  SemiSupervisedClassificationStatisticsAggregator SemiSupervisedClassificationStatisticsAggregator::DeepClone() const
  {
    SemiSupervisedClassificationStatisticsAggregator clone(nClasses_, a_, b_);

    clone.gaussianAggregator2d_ = gaussianAggregator2d_.DeepClone();
    clone.histogramAggregator_ = histogramAggregator_.DeepClone();

    return clone;
  }
}}}
