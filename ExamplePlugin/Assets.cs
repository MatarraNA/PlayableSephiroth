using R2API;
using R2API.AssetPlus;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Sephiroth
{
    public static class Assets
    {
        public static AssetBundle MainAssetBundle = null;
        public static AssetBundleResourcesProvider Provider;

        // FX
        public static GameObject SephHitFx = null;

        // icons
        public static Sprite SephIcon = null;

        public static void PopulateAssets()
        {
            // populate ASSETS
            if(MainAssetBundle == null)
            {
                using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PlayableSephiroth.sephiroth"))
                {
                    MainAssetBundle = AssetBundle.LoadFromStream(assetStream);
                    Provider = new AssetBundleResourcesProvider("@Sephiroth", MainAssetBundle);
                }
            }

            // populate SOUNDS
            using (var bankStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PlayableSephiroth.SephirothBank.bnk"))
            {
                var bytes = new byte[bankStream.Length];
                bankStream.Read(bytes, 0, bytes.Length);
                SoundBanks.Add(bytes);
            }

            // gather assets
            SephIcon = MainAssetBundle.LoadAsset<Sprite>("SephirothIcon");
            SephHitFx = MainAssetBundle.LoadAsset<GameObject>("SephHitFx");
        }
    }
}
