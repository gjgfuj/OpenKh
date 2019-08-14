﻿using System;
using System.IO;
using OpenKh.Common;
using Xe.IO;

namespace OpenKh.Bbs
{
    public partial class Bbsa
    {
        public class Entry
        {
            private const int SectorLength = 0x800;
            private readonly Header bbsaHeader;
            private readonly int offset;
            private readonly int length;
            private readonly string fileName;
            private readonly string folderName;

            internal Entry(
                Bbsa bbsa,
                int offset,
                int length,
                string fileName,
                string folderName,
                uint fileHash,
                uint folderHash)
            {
                bbsaHeader = bbsa._header;
                this.offset = offset;
                this.length = length;
                this.fileName = fileName;
                this.folderName = folderName;
                FileHash = fileHash;
                FolderHash = folderHash;
            }

            public uint FileHash { get; }
            public uint FolderHash { get; }
            public string Name => $"{FolderName}/{FileName}";
            public bool HasCompleteName => fileName != null && folderName != null;

            public string CalculateNameWithExtension(Func<int, Stream> bbsaLoader)
            {
                if (!CalculateArchiveOffset(bbsaHeader, offset, out var archiveIndex, out var physicalSector))
                    return Name;

                var stream = bbsaLoader(archiveIndex);
                var extension = CalculateExtension(stream, physicalSector * SectorLength);
                if (extension == null)
                    return Name;

                return $"{Name}.{extension}";
            }

            public SubStream OpenStream(Func<int, Stream> bbsaLoader)
            {
                if (!CalculateArchiveOffset(bbsaHeader, offset, out var archiveIndex, out var physicalSector))
                    return null;

                var stream = bbsaLoader(archiveIndex);
                var subStreamOffset = physicalSector * SectorLength;
                var subStreamLength = length * SectorLength;

                if (length == 0xFFF)
                {
                    if (IsPsmf(stream, subStreamOffset))
                        subStreamLength = GetPsmfLength(stream, subStreamOffset);
                }

                return new SubStream(stream, subStreamOffset, subStreamLength);
            }

            private string FileName => fileName ?? $"@{FileHash:X08}";
            private string FolderName =>
                folderName ??
                CalculateFolderName(FolderHash) ??
                $"@{FolderHash:X08}";
        }

        private static bool IsPsmf(Stream stream, int offset) =>
            new BinaryReader(stream.SetPosition(offset)).ReadInt32() == 0x464D5350;

        private static int GetPsmfLength(Stream stream, int offset)
        {
            stream.SetPosition(offset + 12);
            return (stream.ReadByte() << 24) |
                (stream.ReadByte() << 16) |
                (stream.ReadByte() << 8) |
                (stream.ReadByte() << 0);
        }

        private static bool CalculateArchiveOffset(
            Header header, int offset, out int archiveIndex, out int physicalSector)
        {
            if (offset >= header.Archive4SectorIndex)
            {
                archiveIndex = 4;
                physicalSector = offset - header.Archive4SectorIndex + 1;
            }
            else if (offset >= header.Archive3SectorIndex)
            {
                archiveIndex = 3;
                physicalSector = offset - header.Archive3SectorIndex + 1;
            }
            else if (offset >= header.Archive2SectorIndex)
            {
                archiveIndex = 2;
                physicalSector = offset - header.Archive2SectorIndex + 1;
            }
            else if (offset >= header.Archive1SectorIndex)
            {
                archiveIndex = 1;
                physicalSector = offset - header.Archive1SectorIndex + 1;
            }
            else if (offset >= header.Archive0SectorIndex)
            {
                archiveIndex = 0;
                physicalSector = offset + header.Archive0SectorIndex;
            }
            else
            {
                archiveIndex = -1;
                physicalSector = -1;
                return false;
            }

            return true;
        }

        private static string CalculateExtension(Stream stream, int offset)
        {
            stream.Position = offset;
            var magicCode = new BinaryReader(stream).ReadUInt32();
            switch (magicCode)
            {
                case 0x61754C1B: return "lub";
                case 0x41264129: return "ice";
                case 0x44544340: return "ctd";
                case 0x50444540: return "edp";
                case 0x00435241: return "arc";
                case 0x44424D40: return "mbd";
                case 0x00444145: return "ead";
                case 0x07504546: return "fep";
                case 0x00425449: return "itb";
                case 0x00435449: return "itc";
                case 0x00455449: return "ite";
                case 0x004D4150: return "pam";
                case 0x004F4D50: return "pmo";
                case 0x42444553: return "scd";
                case 0x324D4954: return "tm2";
                case 0x00415854: return "txa";
                case 0x00617865: return "exa";
                default: return null;
            }
        }

        private static string CalculateFolderName(uint hash)
        {
            var category = hash >> 24;
            var world = (hash >> 16) & 0x1F;
            var language = (hash >> 21) & 7;
            var id = hash & 0xFFFF;

            var strWorld = world < Constants.Worlds.Length ?
                Constants.Worlds[world] : null;
            var strLanguage = language < Constants.Language.Length ?
                Constants.Language[language] : null;

            switch (category)
            {
                case 0x00: return "arc_";
                case 0x80: return "sound/bgm";
                case 0xC0: return "lua";
                case 0x90: return $"sound/se/common";
                case 0x91: return $"sound/se/event/{strWorld}";
                case 0x92: return $"sound/se/footstep/{strWorld}";
                case 0x93: return "sound/se/enemy";
                case 0x94: return "sound/se/weapon";
                case 0x95: return "sound/se/act";
                case 0xA1: return $"sound/voice/{strLanguage}/event/{strWorld}";
                case 0xAA: return $"sound/voice/{strLanguage}/battle";
                case 0xD0: return $"message/{strLanguage}/system";
                case 0xD1: return $"message/{strLanguage}/map";
                case 0xD2: return $"message/{strLanguage}/menu";
                case 0xD3: return $"message/{strLanguage}/event";
                case 0xD4: return $"message/{strLanguage}/mission";
                case 0xD5: return $"message/{strLanguage}/npc_talk/{strWorld}";
                case 0xD6: return $"message/{strLanguage}/network";
                case 0xD7: return $"message/{strLanguage}/battledice";
                case 0xD8: return $"message/{strLanguage}/minigame";
                case 0xD9: return $"message/{strLanguage}/shop";
                case 0xDA: return $"message/{strLanguage}/playerselect";
                case 0xDB: return $"message/{strLanguage}/report";
                default: return null;
            }
        }
    }
}
