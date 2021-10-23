using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace StravaTools
{
    public class Program
    {
        private const int ImageWidth = 1000;
        private const int ImageHeight = 1000;
        //private static readonly double ImageScale = Math.Sqrt(Math.Pow(ImageWidth - 0, 2) + Math.Pow(ImageHeight - 0, 2));

        public static void Main(string[] args)
        {
            var sessions = ExtractSessionsFromFitFile("./5618173276.fit");

            // For now just take the first session
            var session = sessions.First();

            PrintSessionInformation(session);

            // Raw coordinates from fit file
            var rawCoordinates = ExtractRawCoordinates(session);

            // Coordinates shifted to origin
            var shiftedCoordinates = ShiftCoordinatesToOrigin(rawCoordinates);

            // Coordinates scaled to fit image
            var scaledCoordinates = ScaleCoordinatesToImageSize(shiftedCoordinates);

            // Route orientation
            var imageIsHorizontal = scaledCoordinates.Select(coordinate => coordinate.Y).Min() > 1;


            using var image = new Image<Rgba32>(ImageWidth, ImageHeight);
            DrawImage(scaledCoordinates, image, imageIsHorizontal);
            SaveImage(session, image);
        }

        private static void DrawImage(List<PointF> scaledCoordinates, Image<Rgba32> image, bool imageIsHorizontal)
        {
            var path = BuildPath(scaledCoordinates);

            image.Mutate(ctx => ctx
                .Fill(Color.White)
                .Draw(Color.Gray, 5, path));

            if (imageIsHorizontal)
            {
                var minY = scaledCoordinates.Select(coordinate => coordinate.Y).Min();
                image.Mutate(x => x.Crop(new Rectangle(0, (int)minY, ImageWidth, ImageHeight - (int)minY)));
            }
            else
            {
                var minX = scaledCoordinates.Select(coordinate => coordinate.X).Min();
                image.Mutate(x => x.Crop(new Rectangle((int)minX, 0, ImageWidth - (int)minX, ImageHeight)));
            }
        }

        private static IPath BuildPath(List<PointF> scaledCoordinates)
        {
            var pathBuilder = new PathBuilder();
            pathBuilder.SetOrigin(new PointF(0, 0));
            pathBuilder.AddLines(scaledCoordinates);
            IPath path = pathBuilder.Build();
            return path;
        }

        private static void SaveImage(SessionMessages session, Image<Rgba32> image)
        {
            var sport = session.Session.GetSport().ToString() ?? "unknown";
            var distance = (int?)session.Session.GetTotalDistance() ?? 0;
            var date = session.Session.GetStartTime().GetDateTime().ToString("yyyyMMddHH");

            var outputDir = @"C:/code/InnovationDay/StravaTools/images";
            // If directory does not exist, create it
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            image.Save($"{outputDir}/{sport}_{distance}_{date}.png");
        }

        private static List<PointF> ScaleCoordinatesToImageSize(List<PointF> shiftedCoordinates)
        {
            var allX = shiftedCoordinates.Select(coordinate => coordinate.X).ToList();
            var allY = shiftedCoordinates.Select(coordinate => coordinate.Y).ToList();

            var (maxX, maxY) = new PointF(allX.Max(), allY.Max());

            var xDiff = maxX - ImageWidth;
            var yDiff = maxY - ImageHeight;

            var biggestDiff = Math.Max(xDiff, yDiff);
            var xIsBiggestDiff = Math.Abs(biggestDiff - xDiff) < 0.001;

            float maybeScale;
            if (xIsBiggestDiff)
            {
                maybeScale = (ImageWidth / xDiff);
            }
            else
            {
                maybeScale = (ImageWidth / yDiff);
            }

            var normalizedCoordinates = shiftedCoordinates.Select(rawCoordinate =>
            {
                var x = rawCoordinate.X * maybeScale;
                var y = rawCoordinate.Y * maybeScale;
                return new PointF(x, 1000 - y);
            }).ToList();
            return normalizedCoordinates;
        }

        private static List<PointF> ShiftCoordinatesToOrigin(List<PointF> rawCoordinates)
        {
            var allX = rawCoordinates.Select(coordinate => coordinate.X).ToList();
            var allY = rawCoordinates.Select(coordinate => coordinate.Y).ToList();

            var (minX, minY) = new PointF(allX.Min(), allY.Min());

            var shiftedCoordinates = rawCoordinates.Select(rawCoordinate =>
                new PointF(rawCoordinate.X - minX, rawCoordinate.Y - minY)).ToList();
            return shiftedCoordinates;
        }

        private static List<PointF> ExtractRawCoordinates(SessionMessages session)
        {
            var rawCoordinates = new List<PointF>();

            session.Records.ForEach(record =>
            {
                var positionLat = record.GetFieldValue("PositionLat");
                var positionLong = record.GetFieldValue("PositionLong");

                if (positionLong is null || positionLat is null) return;

                var rawCoordinate = new PointF((int)positionLong, (int)positionLat);
                rawCoordinates.Add(rawCoordinate);
            });
            return rawCoordinates;
        }

        private static void PrintSessionInformation(SessionMessages session)
        {
            Console.WriteLine(session.Session.GetSport());
            Console.WriteLine(session.Session.GetTotalDistance());
            Console.WriteLine(session.Session.GetTotalElapsedTime() / 60);
            Console.WriteLine(session.Session.GetStartTime().GetDateTime());
        }

        private static List<SessionMessages> ExtractSessionsFromFitFile(string fileName)
        {
            FileStream fileStream = new FileStream(fileName, FileMode.Open);
            FitDecoder fitDecoder = new FitDecoder(fileStream, Dynastream.Fit.File.Activity);

            try
            {
                fitDecoder.Decode();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                fileStream.Close();
            }

            // Create the Activity Parser and group the messages into individual sessions.
            ActivityParser activityParser = new ActivityParser(fitDecoder.Messages);
            var sessions = activityParser.ParseSessions();
            return sessions;
        }
    }
}
