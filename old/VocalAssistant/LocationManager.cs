using System;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace VocalAssistant
{
    class LocationManager
    {
        public async static Task<Geoposition> GetPosition()
        {
            var access_status = await Geolocator.RequestAccessAsync(); // chiede se può accedere alla geolocalizzazione

            var geolocator = new Geolocator { DesiredAccuracyInMeters = 0 }; // dammi la precisione che riesci
            var position = await geolocator.GetGeopositionAsync();

            return position;
        }
    }
}
