using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ViRMM.Shared.Models;

namespace ViRMM.Agent.Services;

public class ScreenCaptureService
{
    public ScreenFrameDto? CaptureScreen(string agentId, int quality = 65)
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            int width = GetSystemMetrics(SM_CXSCREEN);
            int height = GetSystemMetrics(SM_CYSCREEN);

            if (width <= 0 || height <= 0)
            {
                width = 1920;
                height = 1080;
            }

            using var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(0, 0, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            }

            // Downscale if needed for speed
            int targetWidth = Math.Min(1280, width);
            int targetHeight = (int)((double)height / width * targetWidth);

            using var resized = new Bitmap(bmp, new Size(targetWidth, targetHeight));
            using var ms = new MemoryStream();

            var encoder = GetEncoder(ImageFormat.Jpeg);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);

            if (encoder != null)
            {
                resized.Save(ms, encoder, encoderParams);
            }
            else
            {
                resized.Save(ms, ImageFormat.Jpeg);
            }

            return new ScreenFrameDto
            {
                AgentId = agentId,
                Width = targetWidth,
                Height = targetHeight,
                Base64Image = Convert.ToBase64String(ms.ToArray()),
                TimestampUtcTicks = DateTime.UtcNow.Ticks
            };
        }
        catch
        {
            return null;
        }
    }

    public void SimulateInput(RemoteInputDto input)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            if (input.EventType == "mouse_click" || input.EventType == "mouse_move")
            {
                int screenW = GetSystemMetrics(SM_CXSCREEN);
                int screenH = GetSystemMetrics(SM_CYSCREEN);

                if (screenW > 0 && screenH > 0 && input.X > 0 && input.Y > 0)
                {
                    SetCursorPos(input.X, input.Y);
                }

                if (input.EventType == "mouse_click")
                {
                    if (input.Button == "right")
                    {
                        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                    }
                    else
                    {
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    }
                }
            }
        }
        catch { }
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageDecoders();
        return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid);
    }

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
}
