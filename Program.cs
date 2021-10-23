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

            var rawCoordinates = ExtractRawCoordinates(session);

            var allX = rawCoordinates.Select(coordinate => (int)coordinate.X).ToList();
            var allY = rawCoordinates.Select(coordinate => (int)coordinate.Y).ToList();

            var (minX, minY) = new PointF(allX.Min(), allY.Min());
            var (maxX, maxY) = new PointF(allX.Max(), allY.Max());

            var scale = Math.Sqrt(Math.Pow(maxX - minX, 2) + Math.Pow(maxY - minY, 2));
            //var scale = ImageScale / rawScale;

            var normalizedCoordinates = rawCoordinates.Select(rawCoordinate =>
            {
                var x = ((rawCoordinate.X - minX) / scale) * 1000;
                var y = ((rawCoordinate.Y - minY) / scale) * 1000;
                return new PointF((int)x, (int)y);
            });

            var maxNormalizedX = normalizedCoordinates.Select(x => x.X).Max();
            var maxNormalizedY = normalizedCoordinates.Select(x => x.Y).Max();
            var maxAll = Math.Max(maxNormalizedX, maxNormalizedY);
            bool maxIsX = (int)maxAll == (int)maxNormalizedX;
            var newScale = 1000 / maxAll;

            var scaledCoordinates = normalizedCoordinates.Select(normalizedCoordinates =>
            {
                var x = normalizedCoordinates.X * newScale;
                var y = normalizedCoordinates.Y * newScale;
                return new PointF(x, 1000 - y);
            }).ToList();

            var minYScaled = scaledCoordinates.Select(scaledCoordinate => scaledCoordinate.Y).Min();
            var minXScaled = scaledCoordinates.Select(scaledCoordinate => scaledCoordinate.X).Min();

            using var image = new Image<Rgba32>(ImageWidth, ImageHeight);
            PathBuilder pathBuilder = new PathBuilder();
            pathBuilder.SetOrigin(new PointF(0, 0));
            pathBuilder.AddLines(scaledCoordinates);

            IPath path = pathBuilder.Build();

            image.Mutate<Rgba32>(ctx => ctx
                .Fill(Color.White) // white background image
                .Draw(Color.Gray, 3, path)); // draw the path so we can see what the text is supposed to be following
            if (maxIsX)
            {
                image.Mutate(x => x.Crop(new Rectangle(0, (int)minYScaled, 1000, 1000 - (int)minYScaled)));
            }
            else
            {
                image.Mutate(x => x.Crop(new Rectangle((int)minXScaled, 0, 1000 - (int)minXScaled, 1000)));
            }
            image.Save("C:/code/InnovationDay/StravaTools/strava_tools.png");
        }

        private static List<PointF> ExtractRawCoordinates(SessionMessages session)
        {
            var rawCoordinates = new List<PointF>();

            session.Records.ForEach(record =>
            {
                var positionLat = record.GetFieldValue("PositionLat");
                var positionLong = record.GetFieldValue("PositionLong");

                if (positionLong is null || positionLat is null) return;

                Console.WriteLine("fit-coordinates: {0},{1}", positionLat, positionLong);
                Console.WriteLine();

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
