#include "CumulativeNormalDistribution.h"

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  const double CumulativeNormalDistribution1d::pi = 3.1415926535897932384626433832795;
  const double CumulativeNormalDistribution1d::a1 = 0.319381530;
  const double CumulativeNormalDistribution1d::a2 = -0.356563782;
  const double CumulativeNormalDistribution1d::a3 = 1.781477937;
  const double CumulativeNormalDistribution1d::a4 = -1.821255978;
  const double CumulativeNormalDistribution1d::a5 = 1.330274429;

  const double CumulativeNormalDistribution1d::gamma = 0.2316419;

  const double CumulativeNormalDistribution1d::s = 1.0 / sqrt(2.0 * pi);

  const double CumulativeNormalDistribution2d::A[4] = { 0.3253030, 0.4211071, 0.1334425, 0.006374323 };
  const double CumulativeNormalDistribution2d::B[4] = { 0.1337764, 0.6243247, 1.3425378, 2.2626645 };
} } }
