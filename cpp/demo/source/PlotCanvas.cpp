#include "PlotCanvas.h"

#include <string>

#include "dibCodec.h"

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  template<>
  void Bitmap<PixelBgr>::Save(const std::string& path) const
  {
    encodeDib_BGR_8u (
      &buffer_[0],
      width_, 
      height_,
      width_*sizeof(PixelBgr),
      path.c_str() );
  }
} } }
