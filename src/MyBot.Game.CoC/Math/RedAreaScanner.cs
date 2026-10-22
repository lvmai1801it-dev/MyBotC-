using OpenCvSharp;

namespace MyBot.Game.CoC.Math;

/// <summary>Scans a battle frame for red no-deploy border pixels.</summary>
public class RedAreaScanner
{
    // OpenCV uses BGR, so a red pixel has B &lt; 60, G &lt; 60 and R &gt; 150.
    private static readonly Scalar RedLowerBound = new(0, 0, 150);
    private static readonly Scalar RedUpperBound = new(60, 60, 255);

    /// <summary>Scans the frame and groups red pixels into the four screen quadrants.</summary>
    public RedAreaResult Scan(Mat frame)
    {
        List<Point2D> topLeft = new();
        List<Point2D> topRight = new();
        List<Point2D> bottomLeft = new();
        List<Point2D> bottomRight = new();

        if (frame is null || frame.Empty())
        {
            return new RedAreaResult(topLeft, topRight, bottomLeft, bottomRight);
        }

        using Mat mask = new();
        Cv2.InRange(frame, RedLowerBound, RedUpperBound, mask);

        using Mat points = new();
        Cv2.FindNonZero(mask, points);

        double centerX = frame.Width / 2.0;
        double centerY = frame.Height / 2.0;

        for (int i = 0; i < points.Rows; i++)
        {
            Point pixel = points.At<Point>(i);
            Point2D point = new(pixel.X, pixel.Y);

            if (pixel.X <= centerX)
            {
                if (pixel.Y <= centerY)
                {
                    topLeft.Add(point);
                }
                else
                {
                    bottomLeft.Add(point);
                }
            }
            else
            {
                if (pixel.Y <= centerY)
                {
                    topRight.Add(point);
                }
                else
                {
                    bottomRight.Add(point);
                }
            }
        }

        return new RedAreaResult(topLeft, topRight, bottomLeft, bottomRight);
    }
}
