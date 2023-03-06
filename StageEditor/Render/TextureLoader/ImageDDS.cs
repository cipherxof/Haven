using System;
using System.IO;
using System.Diagnostics;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Haven.TextureLoaders
{
    /// <summary> 
    /// Expects the presence of a valid OpenGL Context and Texture Compression Extensions (GL 1.5) and Cube Maps (GL 1.3).
    /// You will get what you give. No automatic Mipmap generation or automatic compression is done. (both bad quality)
    /// Textures are never rescaled or checked if Power of 2, but you should make the Width and Height a multiple of 4 because DXTn uses 4x4 blocks.
    /// (Image displays correctly but runs extremely slow with non-power-of-two Textures on FX5600, Cache misses?)
    /// CubeMap support is experimental and the file must specify all 6 faces to work at all.
    /// </summary>
    public class ImageDDS
    {

        private const byte HeaderSizeInBytes = 128; // all non-image data together is 128 Bytes
        private const uint BitMask = 0x00000007; // bits = 00 00 01 11

        private static NotImplementedException Unfinished = new NotImplementedException("ERROR: Only 2 Dimensional DXT1/3/5 compressed images for now. 1D/3D Textures may not be compressed according to spec.");

        private static bool _IsCompressed;
        private static int _Width, _Height, _Depth, _MipMapCount;
        private static int _BytesForMainSurface; // must be handled with care when implementing uncompressed formats!
        private static byte _BytesPerBlock;
        private static PixelInternalFormat _PixelInternalFormat;

        [Flags] // Surface Description
        public enum eDDSD : uint
        {
            CAPS = 0x00000001, // is always present
            HEIGHT = 0x00000002, // is always present
            WIDTH = 0x00000004, // is always present
            PITCH = 0x00000008, // is set if the image is uncompressed
            PIXELFORMAT = 0x00001000, // is always present
            MIPMAPCOUNT = 0x00020000, // is set if the image contains MipMaps
            LINEARSIZE = 0x00080000, // is set if the image is compressed
            DEPTH = 0x00800000 // is set for 3D Volume Textures
        }

        [Flags] // Pixelformat 
        public enum eDDPF : uint
        {
            NONE = 0x00000000, // not part of DX, added for convenience
            ALPHAPIXELS = 0x00000001,
            FOURCC = 0x00000004,
            RGB = 0x00000040,
            RGBA = 0x00000041
        }

        /// <summary>This list was derived from nVidia OpenGL SDK</summary>
        [Flags] // Texture types
        public enum eFOURCC : uint
        {
            UNKNOWN = 0,
            DXT1 = 0x31545844,
            DXT2 = 0x32545844,
            DXT3 = 0x33545844,
            DXT4 = 0x34545844,
            DXT5 = 0x35545844,
        }

        [Flags] // dwCaps1
        public enum eDDSCAPS : uint
        {
            NONE = 0x00000000, // not part of DX, added for convenience
            COMPLEX = 0x00000008, // should be set for any DDS file with more than one main surface
            TEXTURE = 0x00001000, // should always be set
            MIPMAP = 0x00400000 // only for files with MipMaps
        }

        [Flags]  // dwCaps2
        public enum eDDSCAPS2 : uint
        {
            NONE = 0x00000000, // not part of DX, added for convenience
            CUBEMAP = 0x00000200,
            CUBEMAP_POSITIVEX = 0x00000400,
            CUBEMAP_NEGATIVEX = 0x00000800,
            CUBEMAP_POSITIVEY = 0x00001000,
            CUBEMAP_NEGATIVEY = 0x00002000,
            CUBEMAP_POSITIVEZ = 0x00004000,
            CUBEMAP_NEGATIVEZ = 0x00008000,
            CUBEMAP_ALL_FACES = 0x0000FC00,
            VOLUME = 0x00200000 // for 3D Textures
        }

        public string idString; // 4 bytes, must be "DDS "
        public UInt32 Size; // Size of structure is 124 bytes, 128 including all sub-structs and the header
        public UInt32 Flags; // Flags to indicate valid fields.
        public UInt32 Height; // Height of the main image in pixels
        public UInt32 Width; // Width of the main image in pixels
        public UInt32 PitchOrLinearSize; // For compressed formats, this is the total number of bytes for the main image.
        public UInt32 Depth; // For volume textures, this is the depth of the volume.
        public UInt32 MipMapCount; // total number of levels in the mipmap chain of the main image.
        public Int32 MipMapOffset; // total number of levels in the mipmap chain of the main image.
        // Pixelformat sub-struct, 32 bytes
        public UInt32 PfSize; // Size of Pixelformat structure. This member must be set to 32.
        public UInt32 PfFlags; // Flags to indicate valid fields.
        public UInt32 PfFourCC; // This is the four-character code for compressed formats.
        // Capabilities sub-struct, 16 bytes
        public static UInt32 dwCaps1; // always includes DDSCAPS_TEXTURE. with more than one main surface DDSCAPS_COMPLEX should also be set.
        public static UInt32 dwCaps2; // For cubic environment maps, DDSCAPS2_CUBEMAP should be included as well as one or more faces of the map (DDSCAPS2_CUBEMAP_POSITIVEX, DDSCAPS2_CUBEMAP_NEGATIVEX, DDSCAPS2_CUBEMAP_POSITIVEY, DDSCAPS2_CUBEMAP_NEGATIVEY, DDSCAPS2_CUBEMAP_POSITIVEZ, DDSCAPS2_CUBEMAP_NEGATIVEZ). For volume textures, DDSCAPS2_VOLUME should be included.
        public byte[] Data;
        /// <summary>
        /// This function will generate, bind and fill a Texture Object with a DXT1/3/5 compressed Texture in .dds Format.
        /// MipMaps below 4x4 Pixel Size are discarded, because DXTn's smallest unit is a 4x4 block of Pixel data.
        /// It will set correct MipMap parameters, Filtering, Wrapping and EnvMode for the Texture. 
        /// The only call inside this function affecting OpenGL State is GL.BindTexture();
        /// </summary>
        /// <param name="filename">The name of the file you wish to load, including path and file extension.</param>
        /// <param name="texturehandle">0 if invalid, otherwise a Texture Object usable with GL.BindTexture().</param>
        /// <param name="dimension">0 if invalid, will output what was loaded (typically Texture1D/2D/3D or Cubemap)</param>
        public ImageDDS(string filename)
        {
            _IsCompressed = false;
            _Width = 0;
            _Height = 0;
            _Depth = 0;
            _MipMapCount = 0;
            _BytesForMainSurface = 0;
            _BytesPerBlock = 0;
            _PixelInternalFormat = PixelInternalFormat.Rgba8;

            try // Exceptions will be thrown if any Problem occurs while working on the file. 
            {
                Data = File.ReadAllBytes(@filename);

                ConvertDX9Header(ref Data); // The first 128 Bytes of the file is non-image data

                // start by checking if all forced flags are present. Flags indicate valid fields, but aren't written by every tool .....
                if (idString != "DDS " || // magic key
                     Size != 124 || // constant size of struct, never reused
                     PfSize != 32 || // constant size of struct, never reused
                     !CheckFlag(Flags, (uint)eDDSD.CAPS) ||        // must know it's caps
                     !CheckFlag(Flags, (uint)eDDSD.PIXELFORMAT) || // must know it's format
                     !CheckFlag(dwCaps1, (uint)eDDSCAPS.TEXTURE)     // must be a Texture
                    )
                    throw new ArgumentException("ERROR: File has invalid signature or missing Flags.");

                if (CheckFlag(Flags, (uint)eDDSD.WIDTH))
                    _Width = (int)Width;
                else
                    throw new ArgumentException("ERROR: Flag for Width not set.");

                if (CheckFlag(Flags, (uint)eDDSD.HEIGHT))
                    _Height = (int)Height;
                else
                    throw new ArgumentException("ERROR: Flag for Height not set.");

                if (CheckFlag(Flags, (uint)eDDSD.DEPTH) && CheckFlag(dwCaps2, (uint)eDDSCAPS2.VOLUME))
                {
                    _Depth = (int)Depth;
                    throw Unfinished;
                }
                else
                {// image is 2D or Cube
                    if (CheckFlag(dwCaps2, (uint)eDDSCAPS2.CUBEMAP))
                    {
                        _Depth = 6;
                    }
                    else
                    {
                        _Depth = 1;
                    }
                }

                // these flags must be set for mipmaps to be included
                if (CheckFlag(dwCaps1, (uint)eDDSCAPS.MIPMAP) && CheckFlag(Flags, (uint)eDDSD.MIPMAPCOUNT))
                    _MipMapCount = (int)MipMapCount; // image contains MipMaps
                else
                    _MipMapCount = 1; // only 1 main image

                // Should never happen
                if (CheckFlag(Flags, (uint)eDDSD.PITCH) && CheckFlag(Flags, (uint)eDDSD.LINEARSIZE))
                    throw new ArgumentException("INVALID: Pitch AND Linear Flags both set. Image cannot be uncompressed and DTXn compressed at the same time.");

                // This flag is set if format is uncompressed RGB RGBA etc.
                if (CheckFlag(Flags, (uint)eDDSD.PITCH))
                {
                    // _BytesForMainSurface = (int) dwPitchOrLinearSize; // holds bytes-per-scanline for uncompressed
                    _IsCompressed = false;
                    throw Unfinished;
                }

                // This flag is set if format is compressed DXTn.
                if (CheckFlag(Flags, (uint)eDDSD.LINEARSIZE))
                {
                    _BytesForMainSurface = (int)PitchOrLinearSize;
                    _IsCompressed = true;
                }

                if (CheckFlag(PfFlags, (uint)eDDPF.FOURCC))
                    switch ((eFOURCC)PfFourCC)
                    {
                        case eFOURCC.DXT1:
                            _PixelInternalFormat = (PixelInternalFormat)ExtTextureCompressionS3tc.CompressedRgbS3tcDxt1Ext;
                            _BytesPerBlock = 8;
                            _IsCompressed = true;
                            break;
                        //case eFOURCC.DXT2:
                        case eFOURCC.DXT3:
                            _PixelInternalFormat = (PixelInternalFormat)ExtTextureCompressionS3tc.CompressedRgbaS3tcDxt3Ext;
                            _BytesPerBlock = 16;
                            _IsCompressed = true;
                            break;
                        //case eFOURCC.DXT4:
                        case eFOURCC.DXT5:
                            _PixelInternalFormat = (PixelInternalFormat)ExtTextureCompressionS3tc.CompressedRgbaS3tcDxt5Ext;
                            _BytesPerBlock = 16;
                            _IsCompressed = true;
                            break;
                        default:
                            throw Unfinished; // handle uncompressed formats 
                    }
                else
                    throw Unfinished;
                // pf*Bitmasks should be examined here

                int Cursor = HeaderSizeInBytes;
                int trueMipMapCount = _MipMapCount - 1;
                // foreach face in the cubemap, get all it's mipmaps levels. Only one iteration for Texture2D

                for (int Level = 0; Level < _MipMapCount; Level++) // start at base image
                {
                    if (Level == 1)
                        MipMapOffset = Cursor;

                    int BlocksPerRow = (_Width + 3) >> 2;
                    int BlocksPerColumn = (_Height + 3) >> 2;
                    int SurfaceBlockCount = BlocksPerRow * BlocksPerColumn; //   // DXTn stores Texels in 4x4 blocks, a Color block is 8 Bytes, an Alpha block is 8 Bytes for DXT3/5
                    int SurfaceSizeInBytes = SurfaceBlockCount * _BytesPerBlock;

                    // skip mipmaps smaller than a 4x4 Pixels block, which is the smallest DXTn unit.
                    if (_Width > 2 && _Height > 2)
                    { // Note: there could be a potential problem with non-power-of-two cube maps
                        //byte[] RawDataOfSurface = new byte[SurfaceSizeInBytes];
                        //Array.Copy(Data, Cursor, RawDataOfSurface, 0, SurfaceSizeInBytes);
                    }
                    else
                    {
                        if (trueMipMapCount > Level)
                            trueMipMapCount = Level - 1; // The current Level is invalid
                    }

                    _Width /= 2;
                    if (_Width < 1)
                        _Width = 1;
                    _Height /= 2;
                    if (_Height < 1)
                        _Height = 1;
                    Cursor += SurfaceSizeInBytes;
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException("ERROR: Exception caught when attempting to load file " + filename + ".\n" + e + "\n" + GetDescriptionFromFile(filename));
                // return; // failure
            }
        }

        private void ConvertDX9Header(ref byte[] input)
        {
            UInt32 offset = 0;
            idString = GetString(ref input, offset);
            offset += 4;
            Size = GetUInt32(ref input, offset);
            offset += 4;
            Flags = GetUInt32(ref input, offset);
            offset += 4;
            Height = GetUInt32(ref input, offset);
            offset += 4;
            Width = GetUInt32(ref input, offset);
            offset += 4;
            PitchOrLinearSize = GetUInt32(ref input, offset);
            offset += 4;
            Depth = GetUInt32(ref input, offset);
            offset += 4;
            MipMapCount = GetUInt32(ref input, offset);
            offset += 4;
            offset += 4 * 11;
            PfSize = GetUInt32(ref input, offset);
            offset += 4;
            PfFlags = GetUInt32(ref input, offset);
            offset += 4;
            PfFourCC = GetUInt32(ref input, offset);
            offset += 4;
            offset += 20;
            dwCaps1 = GetUInt32(ref input, offset);
            offset += 4;
            dwCaps2 = GetUInt32(ref input, offset);
            offset += 4;
            offset += 4 * 3;
        }

        /// <summary> Returns true if the flag is set, false otherwise</summary>
        private static bool CheckFlag(uint variable, uint flag)
        {
            return (variable & flag) > 0 ? true : false;
        }

        private static string GetString(ref byte[] input, uint offset)
        {
            return "" + (char)input[offset + 0] + (char)input[offset + 1] + (char)input[offset + 2] + (char)input[offset + 3];
        }

        private static uint GetUInt32(ref byte[] input, uint offset)
        {
            return (uint)(((input[offset + 3] * 256 + input[offset + 2]) * 256 + input[offset + 1]) * 256 + input[offset + 0]);
        }

        public string GetDescriptionFromFile(string filename)
        {
            return "\n--> Header of " + filename +
                   "\nID: " + idString +
                   "\nSize: " + Size +
                   "\nFlags: " + Flags + " (" + (eDDSD)Flags + ")" +
                   "\nHeight: " + Height +
                   "\nWidth: " + Width +
                   "\nPitch: " + PitchOrLinearSize +
                   "\nDepth: " + Depth +
                   "\nMipMaps: " + MipMapCount +
                   "\n\n---PixelFormat---" + filename +
                   "\nSize: " + PfSize +
                   "\nFlags: " + PfFlags + " (" + (eDDPF)PfFlags + ")" +
                   "\nFourCC: " + PfFourCC + " (" + (eFOURCC)PfFourCC + ")" +
 "\n\n---Capabilities---" + filename +
                   "\nCaps1: " + dwCaps1 + " (" + (eDDSCAPS)dwCaps1 + ")" +
                   "\nCaps2: " + dwCaps2 + " (" + (eDDSCAPS2)dwCaps2 + ")";
        }
    }
}