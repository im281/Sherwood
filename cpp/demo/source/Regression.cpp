#include "Regression.h"

namespace MicrosoftResearch { namespace  Cambridge { namespace Sherwood
{
  const double LinearFitAggregator1d::Pi = 3.1415926535897932384626433832795;
  const double LinearFitAggregator1d::E = 2.7182818284590452353602874713527;

  const PixelBgr RegressionExample::DensityColor = PixelBgr::FromArgb(194, 32, 14);
  const PixelBgr RegressionExample::DataPointColor = PixelBgr::FromArgb(224, 224, 224);
  const PixelBgr RegressionExample::DataPointBorderColor = PixelBgr::FromArgb(0, 0, 0);
  const PixelBgr RegressionExample::MeanColor = PixelBgr::FromArgb(0, 255, 0);
} } }

