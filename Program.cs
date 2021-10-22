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
        private const long CoordinateDivider = 11930465;

        public static void Main(string[] args)
        {

            int width = 1000;
            int height = 1000;
            System.IO.Directory.CreateDirectory("c:/temp");
            using var image = new Image<Rgba32>(width, height);
            PathBuilder pathBuilder = new PathBuilder();
            pathBuilder.SetOrigin(new PointF(0, 0));


            // Attempt to open the input file
            FileStream fileStream = new FileStream("./5618173276.fit", FileMode.Open);
            Console.WriteLine($"Opening {fileStream.Name}");

            // Create our FIT Decoder
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
            var session = sessions.First();

            Console.WriteLine(session.Session.GetSport());
            Console.WriteLine(session.Session.GetTotalDistance());
            Console.WriteLine(session.Session.GetTotalElapsedTime() / 60);
            Console.WriteLine(session.Session.GetStartTime().GetDateTime());

            //session.RecordFieldNames.ToList().ForEach(Console.WriteLine);

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

            var allX = rawCoordinates.Select(coordinate => (int)coordinate.X).ToList();
            var allY = rawCoordinates.Select(coordinate => (int)coordinate.Y).ToList();

            var minX = allX.Min();
            var minY = allY.Min();

            var maxX = allX.Max();
            var maxY = allY.Max();

            Console.WriteLine("Max X,Y: {0},{1}", maxX, maxY);
            Console.WriteLine("Min X,Y: {0},{1}", minX, minY);

            var scale = Math.Sqrt(Math.Pow(maxX - minX, 2) + Math.Pow(maxY - minY, 2));

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
                // scale to y
            }
            image.Save("C:/code/InnovationDay/Strava/StravaTools/strava_tools.png");
        }
    }
}
