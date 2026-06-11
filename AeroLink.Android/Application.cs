using Android.App;
using Android.Runtime;
using System;

namespace AeroLink.Android
{
    [Application]
    public class Application : global::Android.App.Application
    {
        public Application(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }
    }
}
