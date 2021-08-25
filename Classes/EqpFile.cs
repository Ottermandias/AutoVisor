using System.IO;
using System.Linq;
using Lumina.Data;

namespace AutoVisor.Classes
{
    // EQP Structure:
    // 64 x [Block collapsed or not bit]
    // 159 x [EquipmentParameter:ulong]
    // (CountSetBits(Block Collapsed or not) - 1) x 160 x [EquipmentParameter:ulong]
    // Item 0 does not exist and is sent to Item 1 instead.
    // GMP files use the same structure.
    public class EqpFile
    {
        public FileResource File { get; }

        private const ushort EqpParameterSize = 8;
        private const ushort BlockSize        = 160;
        private const ushort TotalBlockCount  = 64;
        private       ushort ExpandedBlockCount { get; set; }

        private readonly ulong[]?[] _blocks = new ulong[TotalBlockCount][];

        private static ushort BlockIdx(ushort idx)
            => (ushort) (idx / BlockSize);

        private static ushort SubIdx(ushort idx)
            => (ushort) (idx % BlockSize);

        public bool ExpandBlock(ushort idx)
        {
            if (idx >= TotalBlockCount || _blocks[idx] != null)
                return false;

            _blocks[idx] = new ulong[BlockSize];
            ++ExpandedBlockCount;
            _blocks[0]![0] |= 1ul << idx;
            return true;
        }

        public bool CollapseBlock(ushort idx)
        {
            if (idx >= TotalBlockCount || _blocks[idx] == null)
                return false;

            _blocks[idx] = null;
            --ExpandedBlockCount;
            _blocks[0]![0] &= ~(1ul << idx);
            return true;
        }

        public void SetEntry(ushort idx, ulong entry)
        {
            var block = BlockIdx(idx);
            if (block >= TotalBlockCount)
                return;

            if (entry != 0)
            {
                ExpandBlock(block);
                _blocks[block]![SubIdx(idx)] = entry;
            }
            else
            {
                var array = _blocks[block];
                if (array == null)
                    return;

                array[SubIdx(idx)] = entry;
                if (array.All(e => e == 0))
                    CollapseBlock(block);
            }
        }

        public byte[] WriteBytes()
        {
            var       dataSize = ExpandedBlockCount * BlockSize * EqpParameterSize;
            using var mem      = new MemoryStream(dataSize);
            using var bw       = new BinaryWriter(mem);

            foreach (var parameter in _blocks.Where(array => array != null)
                .SelectMany(array => array!))
                bw.Write(parameter);

            return mem.ToArray();
        }

        public ulong GetEntry(ushort idx)
        {
            // Skip the zeroth item.
            idx = idx == 0 ? (ushort) 1 : idx;
            var block = BlockIdx(idx);
            var array = block < _blocks.Length ? _blocks[block] : null;
            return array?[SubIdx(idx)] ?? 0;
        }

        public EqpFile(FileResource file)
        {
            File = file;
            file.Reader.BaseStream.Seek(0, SeekOrigin.Begin);
            var blockBits = File.Reader.ReadUInt64();
            // reset to 0 and just put the bitmask in the first block
            // item 0 is not accessible and it simplifies printing.
            file.Reader.BaseStream.Seek(0, SeekOrigin.Begin);

            ExpandedBlockCount = 0;
            for (var i = 0; i < TotalBlockCount; ++i)
            {
                var flag = 1ul << i;
                if ((blockBits & flag) != flag)
                    continue;

                ++ExpandedBlockCount;

                var tmp = new ulong[BlockSize];
                for (var j = 0; j < BlockSize; ++j)
                    tmp[j] = File.Reader.ReadUInt64();

                _blocks[i] = tmp;
            }
        }
    }
}
