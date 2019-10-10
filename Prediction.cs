using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;
using System;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace CustomVision.Lego
{
    class Prediction
    {
        public double Probability { get; }
        public string Tag { get; }
        public string FirstPart { get; }
        public string SecondPart { get; }
        public Prediction(PredictionModel model)
        {
            Probability = model.Probability;
            Tag = model.TagName;

            var parts = Tag.Split('-');
            if (parts.Length >= 2)
            {
                FirstPart = parts[0].ToLower();
                SecondPart = parts[1].ToLower();
            }
        }
        public string Color()
        {
            return FirstPart;
        }

        public bool IsBlock()
        {
            return (SecondPart == "2x4") || (SecondPart == "2x3") || (SecondPart == "2x2") || (SecondPart == "1x4") || (SecondPart == "1x6");
        }

        public bool IsFigure()
        {
            return FirstPart == "figure";
        }

        public BitmapImage GetPicture()
        {
            string picture = "lego";
            if (IsBlock())
            {
                picture = SecondPart;
            }
            else if (IsFigure())
            {
                picture = SecondPart;
            }

            return new BitmapImage(new Uri(String.Format("ms-appx:///Assets/{0}.jpg", picture)));
        }

        public SolidColorBrush GetColorCode()
        {
            switch (FirstPart)
            {
                case "red":
                    return new SolidColorBrush(Colors.Red);
                case "blue":
                    return new SolidColorBrush(Colors.Blue);
                case "green":
                    return new SolidColorBrush(Colors.Green);
                case "lightgreen":
                    return new SolidColorBrush(Colors.LightGreen);
                case "gray":
                    return new SolidColorBrush(Colors.Gray);
                case "white":
                    return new SolidColorBrush(Colors.White);
                case "yellow":
                    return new SolidColorBrush(Colors.Yellow);
                default:
                    return new SolidColorBrush(Colors.LightPink);
            }
        }
    }
}
