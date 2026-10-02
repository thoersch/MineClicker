using UnityEditor;
using UnityEngine;

namespace IdleMine.EditorTools
{
    /// <summary>
    /// Mobile-friendly import settings for everything in Assets/IdleMine/Audio:
    /// short effects are decompressed into memory so they play instantly; music streams as Vorbis.
    /// </summary>
    public class AudioImportSettings : AssetPostprocessor
    {
        const string Root = "Assets/IdleMine/Audio/";

        public override uint GetVersion() { return 1; }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.StartsWith(Root + "Music/");

            var s = importer.defaultSampleSettings;
            if (music)
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.5f;
                importer.loadInBackground = true;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
                importer.forceToMono = true;
            }
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = s;
        }
    }
}
