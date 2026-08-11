namespace MissionPlanner.Maps.Coordinates;

/// <summary>Typed Universal Transverse Mercator coordinate.</summary>
public readonly record struct UtmCoordinate(int Zone, char Hemisphere, double Easting, double Northing);
/// <summary>Typed WGS84 geographic coordinate.</summary>
public readonly record struct GeographicCoordinate(double Latitude, double Longitude);
/// <summary>Converts between WGS84 and UTM.</summary>
public interface IGeodeticCoordinateConverter
{
    /// <summary>Parses `zone hemisphere easting northing`.</summary>
    UtmCoordinate ParseUtm(string text);
    /// <summary>Converts UTM to WGS84.</summary>
    GeographicCoordinate ToGeographic(UtmCoordinate coordinate);
    /// <summary>Converts WGS84 to UTM.</summary>
    UtmCoordinate ToUtm(GeographicCoordinate coordinate);
}

/// <summary>WGS84 UTM forward and inverse converter.</summary>
public sealed class GeodeticCoordinateConverter : IGeodeticCoordinateConverter
{
    private const double A = 6378137d, Eccentricity = 0.0818191908426215d, Scale = 0.9996d;
    /// <inheritdoc />
    public UtmCoordinate ParseUtm(string text)
    { var values = text.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries); if (values.Length != 3 || values[0].Length < 2 || !int.TryParse(values[0][..^1], out var zone) || !double.TryParse(values[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var easting) || !double.TryParse(values[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var northing)) throw new FormatException("Use format '32N 500000 6170000'."); return Validate(new(zone, char.ToUpperInvariant(values[0][^1]), easting, northing)); }
    /// <inheritdoc />
    public GeographicCoordinate ToGeographic(UtmCoordinate coordinate)
    { coordinate = Validate(coordinate); var x=coordinate.Easting-500000d; var y=coordinate.Hemisphere=='N'?coordinate.Northing:coordinate.Northing-10000000d; var e1=(1-Math.Sqrt(1-Eccentricity*Eccentricity))/(1+Math.Sqrt(1-Eccentricity*Eccentricity)); var mu=y/(A*Scale*(1-Eccentricity*Eccentricity/4-3*Math.Pow(Eccentricity,4)/64-5*Math.Pow(Eccentricity,6)/256)); var p=mu+(3*e1/2-27*Math.Pow(e1,3)/32)*Math.Sin(2*mu)+(21*e1*e1/16-55*Math.Pow(e1,4)/32)*Math.Sin(4*mu)+151*Math.Pow(e1,3)/96*Math.Sin(6*mu); var ep=Eccentricity*Eccentricity/(1-Eccentricity*Eccentricity); var n=A/Math.Sqrt(1-Eccentricity*Eccentricity*Math.Sin(p)*Math.Sin(p)); var t=Math.Pow(Math.Tan(p),2); var c=ep*Math.Pow(Math.Cos(p),2); var r=A*(1-Eccentricity*Eccentricity)/Math.Pow(1-Eccentricity*Eccentricity*Math.Sin(p)*Math.Sin(p),1.5); var d=x/(n*Scale); var lat=p-n*Math.Tan(p)/r*(d*d/2-(5+3*t+10*c-4*c*c-9*ep)*Math.Pow(d,4)/24+(61+90*t+298*c+45*t*t-252*ep-3*c*c)*Math.Pow(d,6)/720); var lon=(d-(1+2*t+c)*Math.Pow(d,3)/6+(5-2*c+28*t-3*c*c+8*ep+24*t*t)*Math.Pow(d,5)/120)/Math.Cos(p); return new(lat*180/Math.PI, (coordinate.Zone-1)*6-177+lon*180/Math.PI); }
    /// <inheritdoc />
    public UtmCoordinate ToUtm(GeographicCoordinate coordinate)
    { if (coordinate.Latitude is < -80 or > 84 || coordinate.Longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(coordinate)); var zone=(int)Math.Floor((coordinate.Longitude+180)/6)+1; var origin=(zone-1)*6-177; var lat=coordinate.Latitude*Math.PI/180; var lon=coordinate.Longitude*Math.PI/180; var lon0=origin*Math.PI/180; var ep=Eccentricity*Eccentricity/(1-Eccentricity*Eccentricity); var n=A/Math.Sqrt(1-Eccentricity*Eccentricity*Math.Sin(lat)*Math.Sin(lat)); var t=Math.Pow(Math.Tan(lat),2); var c=ep*Math.Pow(Math.Cos(lat),2); var aa=Math.Cos(lat)*(lon-lon0); var m=A*((1-Eccentricity*Eccentricity/4-3*Math.Pow(Eccentricity,4)/64-5*Math.Pow(Eccentricity,6)/256)*lat-(3*Eccentricity*Eccentricity/8+3*Math.Pow(Eccentricity,4)/32+45*Math.Pow(Eccentricity,6)/1024)*Math.Sin(2*lat)+(15*Math.Pow(Eccentricity,4)/256+45*Math.Pow(Eccentricity,6)/1024)*Math.Sin(4*lat)-35*Math.Pow(Eccentricity,6)/3072*Math.Sin(6*lat)); var e=Scale*n*(aa+(1-t+c)*Math.Pow(aa,3)/6+(5-18*t+t*t+72*c-58*ep)*Math.Pow(aa,5)/120)+500000; var north=Scale*(m+n*Math.Tan(lat)*(aa*aa/2+(5-t+9*c+4*c*c)*Math.Pow(aa,4)/24+(61-58*t+t*t+600*c-330*ep)*Math.Pow(aa,6)/720)); if(coordinate.Latitude<0) north+=10000000; return new(zone,coordinate.Latitude>=0?'N':'S',e,north); }
    private static UtmCoordinate Validate(UtmCoordinate value) => value.Zone is <1 or >60 || value.Hemisphere is not ('N' or 'S') || value.Easting is <100000 or >1000000 || value.Northing is <0 or >10000000 || !double.IsFinite(value.Easting) || !double.IsFinite(value.Northing) ? throw new ArgumentOutOfRangeException(nameof(value), "UTM zone, hemisphere, easting, or northing is invalid.") : value;
}
