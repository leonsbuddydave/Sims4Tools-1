﻿/***************************************************************************
 *  Copyright (C) 2014, 2016 by the Sims 4 Tools development team          *
 *                                                                         *
 *  Contributors:                                                          *
 *  Peter Jones                                                            *
 *  Keyi Zhang                                                             *
 *  CmarNYC                                                                *
 *  Buzzler                                                                *  
 *                                                                         *
 *  This file is part of the Sims 4 Package Interface (s4pi)               *
 *                                                                         *
 *  s4pi is free software: you can redistribute it and/or modify           *
 *  it under the terms of the GNU General Public License as published by   *
 *  the Free Software Foundation, either version 3 of the License, or      *
 *  (at your option) any later version.                                    *
 *                                                                         *
 *  s4pi is distributed in the hope that it will be useful,                *
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of         *
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the          *
 *  GNU General Public License for more details.                           *
 *                                                                         *
 *  You should have received a copy of the GNU General Public License      *
 *  along with s4pi.  If not, see <http://www.gnu.org/licenses/>.          *
 ***************************************************************************/

using System.IO;
using s4pi.Interfaces;
using s4pi.Settings;
using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using s4pi.GenericRCOLResource;
using System.Linq;
using System.Collections;

namespace meshExpImp.ModelBlocks
{
    class BGEO : ARCOLBlock
    {
        static bool checking = s4pi.Settings.Settings.Checking;

        #region Attributes
        uint tag = (uint)FOURCC("BGEO");
        uint version = 0x000000600;
        uint lodCount;
        uint totalVertexCount;
        uint totalVectorCount;
        LodList lodData;
        ushort[] blendMap;
        VectorList vectorData;
        #endregion

        #region Constructors
        public BGEO(int apiVersion, EventHandler handler) : base(apiVersion, handler, null) { }
        public BGEO(int apiVersion, EventHandler handler, Stream s) : base(apiVersion, handler, s) { }
        public BGEO(int apiVersion, EventHandler handler, BGEO basis)
            : this(apiVersion, handler, basis.version, basis.lodCount, basis.totalVertexCount, basis.totalVectorCount,
            basis.lodData, basis.blendMap, basis.vectorData) { }
        public BGEO(int apiVersion, EventHandler handler,
            uint version, uint lodCount, uint totalVertexCount, uint totalVectorCount, LodList lodData, 
            ushort[] blendMap, VectorList vectorData)
            : base(apiVersion, handler, null)
        {
            this.version = version;
            this.lodCount = lodCount;
            this.totalVertexCount = totalVertexCount;
            this.totalVectorCount = totalVectorCount;
            this.lodData = lodData == null ? null : new LodList(handler, lodData);
            this.blendMap = new ushort[blendMap.Length];
            for (int i = 0; i < blendMap.Length; i++)
            {
                this.blendMap[i] = blendMap[i];
            }
            this.vectorData = vectorData == null ? null : new VectorList(handler, vectorData);
        }
        #endregion

        #region ARCOLBlock
        [ElementPriority(2)]
        public override string Tag { get { return "BGEO"; } }

        [ElementPriority(3)]
        public override uint ResourceType { get { return 0x067CAA11; } }
        #endregion

        #region DataIO
        protected override void Parse(Stream s)
        {
            BinaryReader r = new BinaryReader(s);
            this.tag = r.ReadUInt32();
            if (checking) if (tag != (uint)FOURCC("BGEO"))
                    throw new InvalidDataException(String.Format("Invalid Tag read: '{0}'; expected: 'BGEO'; at 0x{1:X8}", FOURCC(tag), s.Position));
            version = r.ReadUInt32();
            if (checking) if (version != 0x00000600)
                    throw new InvalidDataException(String.Format("Invalid Version read: '{0}'; expected: '0x00000600'; at 0x{1:X8}", version, s.Position));
            this.lodCount = r.ReadUInt32();
            this.totalVertexCount = r.ReadUInt32();
            this.totalVectorCount = r.ReadUInt32();
            lodData = new LodList(handler);
            for (int i = 0; i < lodCount; i++)
            {
                lodData.Add(new LOD(this.requestedAPIversion, handler, s));
            }
            blendMap = new ushort[totalVertexCount];
            for (int i = 0; i < totalVertexCount; i++)
            {
                blendMap[i] = r.ReadUInt16();
            }
            vectorData = new VectorList(handler);
            for (int i = 0; i < totalVectorCount; i++)
            {
                vectorData.Add(new Vector(this.requestedAPIversion, handler, s));
            }
        }

        public override Stream UnParse()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ms);

            w.Write(tag);
            w.Write(version);
            if (lodData == null) lodData = new LodList(handler);
            w.Write(lodData.Count);
            if (blendMap == null) blendMap = new ushort[0];
            w.Write(blendMap.Length);
            if (vectorData == null) vectorData = new VectorList(handler);
            w.Write(vectorData.Count);
            for (int i = 0; i < lodData.Count; i++)
            {
                lodData[i].UnParse(ms);
            }
            for (int i = 0; i < blendMap.Length; i++)
            {
                w.Write(blendMap[i]);
            }
            for (int i = 0; i < vectorData.Count; i++)
            {
                vectorData[i].UnParse(ms);
            }
            return ms;
        }

        private byte ReadByte(Stream s) { return new BinaryReader(s).ReadByte(); }
        private void WriteByte(Stream s, byte element) { new BinaryWriter(s).Write(element); }
        #endregion

        #region Sub-Types
        public enum BlendMapFlags
        {
            // Apply a position delta to this vertex.
            FlagPosDelta = 1,
            // Apply a normal delta to this vertex. It follows the position delta if both
            // are present.
            FlagNorDelta = 2,
            FlagAll      = 3,
            PackIndexScale = 4,
        };
        const uint packIndexShift = 2;

        public class LOD : AHandlerElement, IEquatable<LOD>
        {
            const int recommendedApiVersion = 1;

            #region Attributes
            uint indexBase;
            uint numVerts;
            uint numDeltaVectors;
            #endregion

            public LOD(int apiVersion, EventHandler handler) : base(apiVersion, handler) { }
            public LOD(int apiVersion, EventHandler handler, Stream s) : base(apiVersion, handler) { Parse(s); }
            public LOD(int apiVersion, EventHandler handler, LOD basis)
                : this(apiVersion, handler, basis.indexBase, basis.numVerts, basis.numDeltaVectors) { }
            public LOD(int apiVersion, EventHandler handler, uint indexBase, uint numVerts, uint numDeltaVectors)
                : base(apiVersion, handler)
            {
                this.indexBase = indexBase;
                this.numVerts = numVerts;
                this.numDeltaVectors = numDeltaVectors;
            }

            private void Parse(Stream s)
            {
                BinaryReader r = new BinaryReader(s);
                this.indexBase = r.ReadUInt32();
                this.numVerts = r.ReadUInt32();
                this.numDeltaVectors = r.ReadUInt32();
            }
            internal void UnParse(Stream s)
            {
                BinaryWriter w = new BinaryWriter(s);
                w.Write(this.indexBase);
                w.Write(this.numVerts);
                w.Write(this.numDeltaVectors);
            }

            #region AHandlerElement
            public override int RecommendedApiVersion { get { return recommendedApiVersion; } }
            public override List<string> ContentFields { get { return GetContentFields(requestedApiVersion, this.GetType()); } }
            #endregion

            #region IEquatable<LOD>
            public bool Equals(LOD other)
            {
                return this.indexBase.Equals(other.indexBase)
                    && this.numVerts.Equals(other.numVerts)
                    && this.numDeltaVectors.Equals(other.numDeltaVectors);
            }

            public override bool Equals(object obj) { return obj is LOD && Equals(obj as LOD); }

            public override int GetHashCode() { return indexBase.GetHashCode() ^ numVerts.GetHashCode() ^ numDeltaVectors.GetHashCode(); }
            #endregion

            [ElementPriority(1)]
            public uint IndexBase { get { return indexBase; } set { if (indexBase != value) { indexBase = value; OnElementChanged(); } } }
            [ElementPriority(2)]
            public uint NumberVertices { get { return numVerts; } set { if (numVerts != value) { numVerts = value; OnElementChanged(); } } }
            [ElementPriority(3)]
            public uint NumberDeltaVectors { get { return numDeltaVectors; } set { if (numDeltaVectors != value) { numDeltaVectors = value; OnElementChanged(); } } }

            public string Value { get { return string.Join("; ", ValueBuilder.Split('\n')); } }
        }
        public class LodList : DependentList<LOD>
        {
            #region Constructors
            public LodList(EventHandler handler) : base(handler) { }
            public LodList(EventHandler handler, Stream s) : base(handler, s) { }
            public LodList(EventHandler handler, IEnumerable<LOD> le) : base(handler, le) { }
            #endregion

            protected override int ReadCount(Stream s) { return base.ReadCount(s) / 3; }
            protected override LOD CreateElement(Stream s) { return new LOD(0, elementHandler, s); }
          //  protected override void WriteCount(Stream s, int count) { base.WriteCount(s, (int)(count * 3)); }
            protected override void WriteElement(Stream s, LOD element) { element.UnParse(s); }
        }

        public class Vector : AHandlerElement, IEquatable<Vector>
        {
            const int recommendedApiVersion = 1;

            #region Attributes
            ushort[] vector;
            #endregion

            public Vector(int apiVersion, EventHandler handler) : base(apiVersion, handler) { }
            public Vector(int apiVersion, EventHandler handler, Stream s) : base(apiVersion, handler) { Parse(s); }
            public Vector(int apiVersion, EventHandler handler, Vector basis)
                : this(apiVersion, handler, basis.vector) { }
            public Vector(int apiVersion, EventHandler handler, ushort[] vector)
                : base(apiVersion, handler)
            {
                this.vector = new ushort[vector.Length];
                for (int i = 0; i < vector.Length; i++)
                {
                    this.vector[i] = vector[i];
                }
            }

            private void Parse(Stream s)
            {
                BinaryReader r = new BinaryReader(s);
                this.vector = new ushort[3];
                this.vector[0] = r.ReadUInt16();
                this.vector[1] = r.ReadUInt16();
                this.vector[2] = r.ReadUInt16();
            }
            internal void UnParse(Stream s)
            {
                BinaryWriter w = new BinaryWriter(s);
                w.Write(this.vector[0]);
                w.Write(this.vector[1]);
                w.Write(this.vector[2]);
            }

            #region AHandlerElement
            public override int RecommendedApiVersion { get { return recommendedApiVersion; } }
            public override List<string> ContentFields { get { return GetContentFields(requestedApiVersion, this.GetType()); } }
            #endregion

            #region IEquatable<Vector>
            public bool Equals(Vector other)
            {
                return this.vector.SequenceEqual(other.vector);
            }

            public override bool Equals(object obj) { return obj is Vector && Equals(obj as Vector); }

            public override int GetHashCode() { return this.vector.GetHashCode(); }
            #endregion

            [ElementPriority(1)]
            public ushort[] VectorSet { get { return vector; } set { if (vector != value) { vector = value; OnElementChanged(); } } }

            public string Value { get { return string.Join("; ", ValueBuilder.Split('\n')); } }
        }
        public class VectorList : DependentList<Vector>
        {
            #region Constructors
            public VectorList(EventHandler handler) : base(handler) { }
            public VectorList(EventHandler handler, Stream s) : base(handler, s) { }
            public VectorList(EventHandler handler, IEnumerable<Vector> le) : base(handler, le) { }
            #endregion

            protected override int ReadCount(Stream s) { return base.ReadCount(s) / 3; }
            protected override Vector CreateElement(Stream s) { return new Vector(0, elementHandler, s); }
            //  protected override void WriteCount(Stream s, int count) { base.WriteCount(s, (int)(count * 3)); }
            protected override void WriteElement(Stream s, Vector element) { element.UnParse(s); }
        }
        #endregion

        #region Content Fields
        [ElementPriority(1)]
        public uint Version { get { return version; } set { if (version != value) { version = value; OnRCOLChanged(this, EventArgs.Empty); } } }
        [ElementPriority(2)]
        public LodList LodData { get { return lodData; } set { if (lodData != value) { lodData = value; OnRCOLChanged(this, EventArgs.Empty); } } }
        [ElementPriority(3)]
        public ushort[] BlendMap { get { return blendMap; } set { if (blendMap != value) { blendMap = value; OnRCOLChanged(this, EventArgs.Empty); } } }
        [ElementPriority(4)]
        public VectorList VectorData { get { return vectorData; } set { if (vectorData != value) { vectorData = value; OnRCOLChanged(this, EventArgs.Empty); } } }

        public string Value { get { return ValueBuilder; } }
        #endregion
    }
}