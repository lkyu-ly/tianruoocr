using ShareX.ScreenCaptureLib;

namespace TrOCR.Services.ScreenCapture
{
    public interface IScreenCaptureService
    {
        ScreenCaptureResult CaptureForOcr(RegionCaptureOptions options);
    }
}
