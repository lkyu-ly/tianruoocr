using ShareX.ScreenCaptureLib;

namespace TrOCR.Services.ScreenCapture
{
    public sealed class ShareXScreenCaptureService : IScreenCaptureService
    {
        public ScreenCaptureResult CaptureForOcr(RegionCaptureOptions options)
        {
            var image = RegionCaptureTasks.GetRegionImage_Mo(
                options,
                out var modeFlag,
                out var point,
                out var rectangles);

            return new ScreenCaptureResult(image, modeFlag, point, rectangles);
        }
    }
}
