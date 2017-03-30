#include "Classification.h"

#include "FeatureResponseFunctions.h"

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  AxisAlignedFeatureResponse AxisAlignedFeatureResponseFactory::CreateRandom(Random& random)
  {
    return AxisAlignedFeatureResponse::CreateRandom(random);
  }

  LinearFeatureResponse2d LinearFeatureFactory::CreateRandom(Random& random)
  {
    return LinearFeatureResponse2d::CreateRandom(random);
  }
} } }

