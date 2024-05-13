using System;
using Windows.UI;

namespace Transparency.Models;

public class NamedColor : ICloneable
{
    /// <summary>
    /// The time in which the event was created.
    /// </summary>
    public string? Time { get; set; }

    /// <summary>
    /// CPU use percentage.
    /// </summary>
    public string? Amount { get; set; }

    /// <summary>
    /// Bar size.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Bar opacity.
    /// </summary>
    public double Opacity { get; set; }

    /// <summary>
    /// The color associated with the usage.
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// Support for deep-copy routines.
    /// </summary>
    public object Clone()
    {
        return this.MemberwiseClone();
        //return new NamedColor
        //{
        //    Time = this.Time,
        //    Amount = this.Amount,
        //    Height = this.Height,
        //    Color = this.Color,
        //    Opacity = this.Opacity,
        //};
    }

    public override string ToString()
    {
        string format = "Time:{0,-20} Amount:{1,-20} Color:{2,-10} Height:{3,-10} Opacity:{4,-10}";
        return String.Format(format, $"{Time}", $"{Amount}", $"{Color}", $"{Height}", $"{Opacity}");
    }
}
