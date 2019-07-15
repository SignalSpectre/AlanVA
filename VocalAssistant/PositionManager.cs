using System;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;


namespace VocalAssistant
{
    class PositionManager
    {
        public async static Task<Geoposition> GetPosition()
        {
            var access_status = await Geolocator.RequestAccessAsync();
            var geolocator = new Geolocator { DesiredAccuracyInMeters = 0 }; // Be as precise as possible
            var position = await geolocator.GetGeopositionAsync();
            return position;
        }
    }
}