using System.Runtime.InteropServices;
using OpenCvSharp;

namespace MyBot.Core.CV.Native;

/// <summary>
/// Ảnh DIB 32-bit top-down dùng làm <c>HBITMAP</c> cho các export của
/// <c>MyBot.run.dll</c> (<c>ocr</c>, <c>FindTile</c>, ...).
/// </summary>
internal sealed class NativeBitmap : IDisposable
{
    private readonly IntPtr _handle;

    private NativeBitmap(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>Gets handle của HBITMAP.</summary>
    public IntPtr Handle => _handle;

    /// <summary>Tạo DIB section từ <see cref="Mat"/> và copy pixel BGRA vào đó.</summary>
    /// <param name="mat">Ảnh nguồn (8-bit, 1/3/4 kênh).</param>
    /// <returns>Đối tượng cần được dispose.</returns>
    public static NativeBitmap FromMat(Mat mat)
    {
        ArgumentNullException.ThrowIfNull(mat);

        if (mat.Depth() != MatType.CV_8U)
        {
            throw new NotSupportedException("Chỉ hỗ trợ ảnh 8-bit.");
        }

        using Mat bgra = ToBgra(mat);
        int width = bgra.Width;
        int height = bgra.Height;
        int stride = checked(width * 4);

        var header = new BitmapInfoHeader
        {
            Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
            Width = width,
            Height = -height,
            Planes = 1,
            BitCount = 32,
            Compression = 0,
            SizeImage = (uint)(stride * height),
        };

        IntPtr handle = CreateDIBSection(IntPtr.Zero, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
        if (handle == IntPtr.Zero || bits == IntPtr.Zero)
        {
            throw new InvalidOperationException("Không tạo được DIB section cho ảnh.");
        }

        try
        {
            byte[] row = new byte[stride];
            long sourceStep = bgra.Step();

            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(IntPtr.Add(bgra.Data, checked((int)(y * sourceStep))), row, 0, stride);
                Marshal.Copy(row, 0, IntPtr.Add(bits, y * stride), stride);
            }
        }
        catch
        {
            _ = DeleteObject(handle);
            throw;
        }

        return new NativeBitmap(handle);
    }

    /// <summary>Giải phóng HBITMAP.</summary>
    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            _ = DeleteObject(_handle);
        }
    }

    private static Mat ToBgra(Mat mat)
    {
        var bgra = new Mat();
        switch (mat.Channels())
        {
            case 1:
                Cv2.CvtColor(mat, bgra, ColorConversionCodes.GRAY2BGRA);
                break;
            case 3:
                Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);
                break;
            case 4:
                mat.CopyTo(bgra);
                break;
            default:
                bgra.Dispose();
                throw new NotSupportedException($"Số kênh ảnh không hỗ trợ: {mat.Channels()}.");
        }

        return bgra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BitmapInfoHeader pbmi,
        uint usage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint offset);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);
}
