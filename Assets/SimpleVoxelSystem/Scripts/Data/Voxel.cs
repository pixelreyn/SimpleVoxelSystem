
namespace PixelReyn.SimpleVoxelSystem
{
    public class Voxel
    {
        public short Data;

        public Voxel(sbyte id, bool transparent, bool isStatic, byte size = 0)
        {
            Data = (short)(id & 0b0011_1111); // Assign ID and ensure it fits in 6 bits
            Transparent = transparent;
            Static = isStatic;
            Size = size;
        }

        public Voxel(short data)
        {
            Data = data;
        }

         public int Id
        {
            get { return Data & 0x3F; } // Get the first 6 bits
            set
            {
                Data = (short)((Data & ~0x3F) | (value & 0x3F));
            }
        }

        public bool Transparent
        {
            get { return (Data & 0x40) != 0; }
            set
            {
                if (value) Data |= 0x40;
                else Data &= (short)~0x40;
            }
        }

        public bool Static
        {
            get { return (Data & 0x80) != 0; }
            set
            {
                if (value) Data |= 0x80;
                else Data &= (short)~0x80;
            }
        }
        public byte Size 
        {
            get { return (byte)((Data >> 8) & 0x07); } // Extract bits 8-10 for size
            set
            {
                // Ensure value fits in 3 bits
                value &= 0x07;
                // Clear bits 8-10
                Data &= (short)~(0x07 << 8);
                // Set new size value
                Data |= (short)(value << 8);
            }
        }
    }
}