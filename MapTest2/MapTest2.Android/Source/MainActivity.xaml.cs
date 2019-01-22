// Note: distribution requires MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Forms.Maps;

using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Gms.Maps.Model;

using Plugin.CurrentActivity;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using Xamarin.Forms.Maps.Android;
using Android.Content;

namespace MapTest2.Droid
{
    public class Test
    {
        public
            static Position a = new Position(47.9220033, -122.22861);
            static Position b = new Position(47.0143142, -120.5318273);
            static Position c = new Position(47.6548883, -122.3095367);

            // Round trip from some bus stop in Everett to all over
            static string request = "https://maps.googleapis.com/maps/api/directions/json?" +
                "origin=" + a.Latitude + "," + a.Longitude + "&destination=" + a.Latitude + "," + a.Longitude +
                "&waypoints=" + b.Latitude + "," + b.Longitude + "|" + c.Latitude + "," + c.Longitude +
                "&key=AIzaSyCaF4A-XmTci3nUg30ppLFezWaIqj8T-qo";
    }

    // This class is based off information here: https://docs.microsoft.com/en-us/xamarin/xamarin-forms/app-fundamentals/custom-renderer/map/polyline-map-overlay
    public class KCMap : Map
    {
        public List<Position> RouteCoordinates { get; set; }

        public KCMap()
        {
            RouteCoordinates = new List<Position>();
        }
        // This code is modeled off of a post by gtleal here: https://forums.xamarin.com/discussion/85684/how-can-i-draw-polyline-for-an-encoded-points-string
        // and based on information here: https://developers.google.com/maps/documentation/utilities/polylinealgorithm
        private List<Position> DecodePolyline(string encodedPoints)
        {
            if (string.IsNullOrWhiteSpace(encodedPoints))
            {
                return null;
            }

            int index = 0;
            var polylineChars = encodedPoints.ToCharArray();
            var poly = new List<Position>();
            int currentLat = 0;
            int currentLng = 0;
            int next5Bits;

            while (index < polylineChars.Length)
            {
                // calculate next latitude
                int sum = 0;
                int shifter = 0;

                do
                {
                    next5Bits = polylineChars[index++] - 63;
                    sum |= (next5Bits & 31) << shifter;
                    shifter += 5;
                }
                while (next5Bits >= 32 && index < polylineChars.Length);

                if (index >= polylineChars.Length)
                {
                    break;
                }

                currentLat += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                // calculate next longitude
                sum = 0;
                shifter = 0;

                do
                {
                    next5Bits = polylineChars[index++] - 63;
                    sum |= (next5Bits & 31) << shifter;
                    shifter += 5;
                }
                while (next5Bits >= 32 && index < polylineChars.Length);

                if (index >= polylineChars.Length && next5Bits >= 32)
                {
                    break;
                }

                currentLng += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                var pos = new Position(Convert.ToDouble(currentLat) / 100000.0, Convert.ToDouble(currentLng) / 100000.0);
                poly.Add(pos);
            }

            return poly;
        }
    }

    // Code here taken from Permission Plugin intstructions
    [Activity(Label = "MapTest2", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            Plugin.CurrentActivity.CrossCurrentActivity.Current.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);
            Xamarin.FormsMaps.Init(this, savedInstanceState);

            var width = Resources.DisplayMetrics.WidthPixels;
            var height = Resources.DisplayMetrics.HeightPixels;
            var density = Resources.DisplayMetrics.Density;

            KCApp.ScreenWidth = (width - 0.5f) / density;
            KCApp.ScreenHeight = (height - 0.5f) / density;

            LoadApplication(new KCApp());
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            PermissionsImplementation.Current.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }

    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class KCApp : Xamarin.Forms.Application
    {
        public static double ScreenHeight;
        public static double ScreenWidth;

        public KCApp()
        {
            InitializeComponent();

            var navPage = new NavigationPage(new ContentPage());
            MainPage = navPage; 

            if (Task.Run(() => this.getLocationPermission()).Result)
            {
                Task.Run(() => navPage.PushAsync(new MapPage())).Wait();
            }
        }

        async Task<bool> getLocationPermission()
        {
            // Code modeled after Permission Plugin example: https://github.com/jamesmontemagno/PermissionsPlugin
            try
            {
                var status = await CrossPermissions.Current.CheckPermissionStatusAsync(Plugin.Permissions.Abstractions.Permission.Location);
                if (status != PermissionStatus.Granted)
                {
                    if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Plugin.Permissions.Abstractions.Permission.Location))
                    {
                        await MainPage.DisplayAlert("Location required.", "Location required for navigation animation.", "OK");
                    }

                    var results = await CrossPermissions.Current.RequestPermissionsAsync(Plugin.Permissions.Abstractions.Permission.Location);

                    if (results.ContainsKey(Plugin.Permissions.Abstractions.Permission.Location))
                        status = results[Plugin.Permissions.Abstractions.Permission.Location];
                }

                if (status == PermissionStatus.Granted)
                {
                    return true;
                }
                else
                {
                    await MainPage.DisplayAlert("Permission Denied", "Location permission is required. Please restart.", "OK");
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        protected override void OnStart()
        {
            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }

    // All code here taken from Microsoft's page on their Map interface: https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/map
    public class MapPage : ContentPage
    {
        public MapPage()
        {
            var map = new KCMap
            {
                MapType = MapType.Street,
                WidthRequest = KCApp.ScreenWidth,
                HeightRequest = KCApp.ScreenHeight
            };

            

            map.MoveToRegion(MapSpan.FromCenterAndRadius(Test.a, Distance.FromMiles(1.0)));
            Content = map;
        }
    };
}