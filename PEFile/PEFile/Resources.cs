using System;
using System.Collections.Generic;
using System.IO;

namespace PEFile
{
    #region Cursor
    struct RT_Cursor_Header
    {
        public UInt16 CDReserved;  // 保留字段，总是为0
        public UInt16 CDType;      // 光标资源类型，总是为2
        public UInt16 CDCount;     // 光标个数
        public Cursor_Direntry[] CursorDirectories; // 数组长度由CDCount决定
        
        public RT_Cursor_Header(byte[] buff, PEFileInfo peFile)
        {
            CursorDirectories = null;
            CDReserved = (UInt16)(((0xffff & buff[1]) << 8) + buff[0]);
            CDType = (UInt16)(((0xffff & buff[3]) << 8) + buff[2]);
            CDCount = (UInt16)(((0xffff & buff[5]) << 8) + buff[4]);

            if (CDCount > 0)
            {
                CursorDirectories = new Cursor_Direntry[CDCount];
            }

            int offset = 0;
            for (int i = 0; i < CDCount;i++ )
            {
                offset = i * 16;
                CursorDirectories[i] = new Cursor_Direntry(buff, offset, peFile);
            }
                
        }
    }

    struct Cursor_Direntry
    {
        public byte Width;
        public byte Height;
        public byte ColorCount;
        public byte Reserved;
        public UInt16 HotspotX;
        public UInt16 HotspotY;
        public UInt32 BytesInRes;
        public UInt32 ImageOffset;

        public Cursor_Direntry(byte[] buff, int offset, PEFileInfo peFile)
        {
            Width = buff[offset];
            Height = buff[offset + 1];
            ColorCount = buff[offset + 2];
            Reserved = buff[offset + 3];
            HotspotX = (UInt16)(((0xffff & buff[offset + 5]) << 8) + buff[offset + 4]);
            HotspotY = (UInt16)(((0xffff & buff[offset + 7]) << 8) + buff[offset + 6]);
            BytesInRes = (0xffffffff & buff[offset + 8]) +
                             ((0xffffffff & buff[offset + 9]) << 8) +
                             ((0xffffffff & buff[offset + 10]) << 16) +
                             ((0xffffffff & buff[offset + 11]) << 24);
            ImageOffset = (0xffffffff & buff[offset + 12]) +
                 ((0xffffffff & buff[offset + 13]) << 8) +
                 ((0xffffffff & buff[offset + 14]) << 16) +
                 ((0xffffffff & buff[offset + 15]) << 24);
        }
     }


    #endregion

    #region String Table

    // 字符串资源是一个字符串表，包含16个字符串
    // 每个字符串结构为[Length][Length个字符]，Length部分占2字节，字符部分每个字符占2字节。
    // 如果Length为0，则字符部分不占字节，如果Length不为0，则字符部分占 2 * Length字节。
    // 比如，如果某个字符串表，包含14个长度为0的字符串和2个长度为18的字符串，则该字符串表总字节数：14 * 2 + 2 * (18 + 2) = 68字节
    struct Resource_String_Table
    {
        public Resource_String[] StringTable;
        public Resource_String_Table(byte[] buff)
        {
            StringTable = new Resource_String[16];
            int offset = 0;
            for (int i = 0; i < 16; i++)
            {
                UInt16 len = (UInt16)(((0xffff & buff[offset + 1]) << 8) + buff[offset]);
                StringTable[i].Length = len;
                offset += 2;
                if (len > 0)
                {
                    StringTable[i].Data = new byte[2 * len];
                    for (int j = 0; j < 2 * len; j++)
                    {
                        StringTable[i].Data[j] = buff[offset];
                        offset++;
                    }
                }
            }
        }
    }

    struct Resource_String  // Resource Type = 6
    {
        public UInt16 Length;
        public byte[] Data;

        public override string ToString()
        {
            if (Length == 0)
            {
                return "";
            }
            else
            {
                char[] ret = new Char[Length];
                for(int i=0;i<Length;i++)
                {
                    ret[i] = (char)(((0xffff & Data[2 * i + 1]) << 8) + Data[2 * i]);
                }

                string output = new string(ret);
                return new string(ret);
            }
        }
    }
    #endregion

    #region Version Info
    struct VS_VERSION_INFO
    {
        public UInt16 Length;
        public UInt16 ValueLength;
        public UInt16 Type;  // 0: Binary, 1: String, Information Type，不起作用
        public Char[] Key;   // 长度为16，此值永远是：VS_VERSION_INFO
        public UInt16 Padding;  
        public VS_FIXEDFILEINFO Value;
        public VersionFileInfo[] Children;  // 由于数组成员类型不一样，因此，用一个{类型，数值}的结构体数组来实现

        public VS_VERSION_INFO(byte[] buff)
        {
            Length = (UInt16)(((0xffff & buff[1]) << 8) + buff[0]);
            ValueLength = (UInt16)(((0xffff & buff[3]) << 8) + buff[2]);
            Type = (UInt16)(((0xffff & buff[5]) << 8) + buff[4]);
            Key = new char[16];
            for (int i = 0; i < Key.Length; i++)
            {
                Key[i] = (char)(((0xffff & buff[2*i + 7]) << 8) + buff[2*i + 6]);
            }
            Padding = (UInt16)(((0xffff & buff[39]) << 8) + buff[38]);
            Value = new VS_FIXEDFILEINFO(buff, 40);  // 该结构体共52字节

            // 把StringFileInfo和VarFileInfo全部按顺序存入链表
            Link child = new Link();
            Link currentNode = child;
            int currentPos = 92;
            while (currentPos < buff.Length)
            {
                UInt16 fileInfoType = (char)(((0xffff & buff[currentPos + 7]) << 8) + buff[currentPos + 6]);
                // 接下来是以 StringFileInfo 或者 VarFileInfo 为开始标志的数据部分，这里简略判断第一字符是否是以“S”开头。
                if (fileInfoType == 'S')
                {
                    StringFileInfo sfi = new StringFileInfo(buff, currentPos);
                    currentNode.Value = sfi;
                    currentNode.NodeType = 1;
                    currentNode.Next = new Link();
                    currentNode = currentNode.Next;
                    child.NodeCount += 1;
                    currentPos += sfi.Length;
                    if ((currentPos % 4) != 0)
                    {
                        currentPos += 2; // 按4字节对齐
                    }
                }
                else
                {
                    VarFileInfo vfi = new VarFileInfo(buff, currentPos);
                    currentNode.Value = vfi;
                    currentNode.NodeType = 0;
                    child.NodeCount += 1;
                    currentNode.Next = new Link();
                    currentNode = currentNode.Next;
                    currentPos += vfi.Length;
                    if ((currentPos % 4) != 0)
                    {
                        currentPos += 2; // 按4字节对齐
                    }
                } 
            }

            // 把存入链表的StringFileInfo和VarFileInfo按顺序拷贝到数组里
            Children = null;
            if (child.NodeCount > 0)
            {
                Children = new VersionFileInfo[child.NodeCount];
                currentNode = child;
                for (int i = 0; i < Children.Length; i++)
                {
                    Children[i].InfoType = currentNode.NodeType;
                    Children[i].Value = currentNode.Value;
                    currentNode = currentNode.Next;
                }
            }
        }

        public void Export(string file)
        {
            string temp = "VS_VERSION_INFO\r\n";
            temp += "Length :" + Length.ToString() + "\r\n";
            temp += "ValueLength :" + ValueLength.ToString() + "\r\n";
            temp += "Type :" + Type.ToString() + "\r\n";
            temp += "Key :" + new String(Key) + "\r\n";
            temp += "Padding :" + Padding.ToString() + "\r\n";
            temp += "\r\n";
            File.WriteAllText(file, temp);
            Value.Export(file);
            for (int i=0;i<Children.Length;i++)
            {
                if (Children[i].InfoType == 1)
                {
                    ((StringFileInfo)Children[i].Value).Export(file);
                }
                else
                {
                    ((VarFileInfo)Children[i].Value).Export(file);
                }
            }
        }
    }
    struct VS_FIXEDFILEINFO
    {
        public UInt32 Signature;       
        public UInt32 StrucVersion;    
        public UInt32 FileVersionMS;   
        public UInt32 FileVersionLS;   
        public UInt32 ProductVersionMS;
        public UInt32 ProductVersionLS;
        public UInt32 FileFlagsMask;   
        public UInt32 FileFlags;       
        public UInt32 FileOS;          
        public UInt32 FileType;        
        public UInt32 FileSubtype;     
        public UInt32 FileDateMS;      
        public UInt32 FileDateLS;
        public VS_FIXEDFILEINFO(byte[] buff, long offset)
        {
            Signature = (0xffffffff & buff[offset]) +
                        ((0xffffffff & buff[offset + 1]) << 8) +
                        ((0xffffffff & buff[offset + 2]) << 16) +
                        ((0xffffffff & buff[offset + 3]) << 24);
            StrucVersion = (0xffffffff & buff[offset + 4]) +
                        ((0xffffffff & buff[offset + 5]) << 8) +
                        ((0xffffffff & buff[offset + 6]) << 16) +
                        ((0xffffffff & buff[offset + 7]) << 24);
            FileVersionMS = (0xffffffff & buff[offset + 8]) +
                        ((0xffffffff & buff[offset + 9]) << 8) +
                        ((0xffffffff & buff[offset + 10]) << 16) +
                        ((0xffffffff & buff[offset + 11]) << 24);
            FileVersionLS = (0xffffffff & buff[offset + 12]) +
                        ((0xffffffff & buff[offset + 13]) << 8) +
                        ((0xffffffff & buff[offset + 14]) << 16) +
                        ((0xffffffff & buff[offset + 15]) << 24);
            ProductVersionMS = (0xffffffff & buff[offset + 16]) +
                        ((0xffffffff & buff[offset + 17]) << 8) +
                        ((0xffffffff & buff[offset + 18]) << 16) +
                        ((0xffffffff & buff[offset + 19]) << 24);
            ProductVersionLS = (0xffffffff & buff[offset + 20]) +
                        ((0xffffffff & buff[offset + 21]) << 8) +
                        ((0xffffffff & buff[offset + 22]) << 16) +
                        ((0xffffffff & buff[offset + 23]) << 24);
            FileFlagsMask = (0xffffffff & buff[offset + 24]) +
                        ((0xffffffff & buff[offset + 25]) << 8) +
                        ((0xffffffff & buff[offset + 26]) << 16) +
                        ((0xffffffff & buff[offset + 27]) << 24);
            FileFlags = (0xffffffff & buff[offset + 28]) +
                        ((0xffffffff & buff[offset + 29]) << 8) +
                        ((0xffffffff & buff[offset + 30]) << 16) +
                        ((0xffffffff & buff[offset + 31]) << 24);
            FileOS = (0xffffffff & buff[offset + 32]) +
                        ((0xffffffff & buff[offset + 33]) << 8) +
                        ((0xffffffff & buff[offset + 34]) << 16) +
                        ((0xffffffff & buff[offset + 35]) << 24);
            FileType = (0xffffffff & buff[offset + 36]) +
                        ((0xffffffff & buff[offset + 37]) << 8) +
                        ((0xffffffff & buff[offset + 38]) << 16) +
                        ((0xffffffff & buff[offset + 39]) << 24);
            FileSubtype = (0xffffffff & buff[offset + 40]) +
                        ((0xffffffff & buff[offset + 41]) << 8) +
                        ((0xffffffff & buff[offset + 42]) << 16) +
                        ((0xffffffff & buff[offset + 43]) << 24);
            FileDateMS = (0xffffffff & buff[offset + 44]) +
                        ((0xffffffff & buff[offset + 45]) << 8) +
                        ((0xffffffff & buff[offset + 46]) << 16) +
                        ((0xffffffff & buff[offset + 47]) << 24);
            FileDateLS = (0xffffffff & buff[offset + 48]) +
                        ((0xffffffff & buff[offset + 49]) << 8) +
                        ((0xffffffff & buff[offset + 50]) << 16) +
                        ((0xffffffff & buff[offset + 51]) << 24);
        }

        public void Export(string file)
        {
            string temp = "VS_FIXEDFILEINFO\r\n";
            temp += "Signature :0x" + Signature.ToString("x8") + "\r\n";
            temp += "StrucVersion :0x" + StrucVersion.ToString("x8") + "\r\n";
            temp += "FileVersionMS :0x" + FileVersionMS.ToString("x8") + "\r\n";
            temp += "FileVersionLS :0x" + FileVersionLS.ToString("x8") + "\r\n";
            temp += "ProductVersionMS :0x" + ProductVersionMS.ToString("x8") + "\r\n";
            temp += "ProductVersionLS :0x" + ProductVersionLS.ToString("x8") + "\r\n";
            temp += "FileFlagsMask :0x" + FileFlagsMask.ToString("x8") + "\r\n";
            temp += "FileFlags :0x" + FileFlags.ToString("x8") + "\r\n";
            temp += "FileOS :0x" + FileOS.ToString("x8") + "\r\n";
            temp += "FileType :0x" + FileType.ToString("x8") + "\r\n";
            temp += "FileSubtype :0x" + FileSubtype.ToString("x8") + "\r\n";
            temp += "FileDateMS :0x" + FileDateMS.ToString("x8") + "\r\n";
            temp += "FileDateLS :0x" + FileDateLS.ToString("x8") + "\r\n";
            temp += "\r\n";
            File.AppendAllText(file, temp);
        }
    }
    struct StringFileInfo
    {
        public UInt16 Length;
        public UInt16 ValueLength;
        public UInt16 Type;  // 貌似永远为1
        public char[] Key;  // 此处总是为: StringFileInfo
        public UInt16 Padding;
        public StringTable[] Children;

        public StringFileInfo(byte[] buff, int offset)
        {
            Length = (UInt16)(((0xffff & buff[offset + 1]) << 8) + buff[offset]);
            ValueLength = (UInt16)(((0xffff & buff[offset + 3]) << 8) + buff[offset + 2]);
            Type = (UInt16)(((0xffff & buff[offset + 5]) << 8) + buff[offset + 4]);
            Key = new Char[14];
            for (int i = 0; i < 14; i++)
            {
                Key[i] = (char)(((0xffff & buff[offset + 7 + 2 * i]) << 8) + buff[offset + 6 + i * 2]);
            }
            Padding = (UInt16)(((0xffff & buff[offset + 35]) << 8) + buff[offset + 34]);
            int pos = offset + 36;
            int tableCount = 0;
            while (pos < offset + Length)  // 计算一共有多少个StringTable，貌似永远是两个
            {
                tableCount++;
                int len = (UInt16)(((0xffff & buff[pos + 1]) << 8) + buff[pos]);
                if (len == 0) break;
                pos += len;
                if ((pos % 4) > 0)
                {
                    pos += 2;// 每个Table按4字节对齐
                }
            }
            Children = new StringTable[tableCount];
            pos = offset + 36;
            for (int i = 0; i < Children.Length; i++)
            {
                Children[i] = new StringTable(buff, pos);
                int len = (UInt16)(((0xffff & buff[pos + 1]) << 8) + buff[pos]);
                pos += len;
                if ((pos % 4) > 0)
                {
                    pos += 2;// 每个Table按4字节对齐
                }
            }
        }

        public void Export(string file)
        {
            string temp = "String File Info \r\n";
            temp += "Length :" + Length.ToString() + "\r\n";
            temp += "ValueLength :" + ValueLength.ToString() + "\r\n";
            temp += "Type :" + Type.ToString() + "\r\n";
            temp += "Key :" + new String(Key) + "\r\n";
            temp += "Padding :" + Padding.ToString() + "\r\n";
            temp += "\r\n";
            File.AppendAllText(file, temp);
            for (int i = 0; i < Children.Length; i++)
            {
                Children[i].Export(file);
            }
        }
    }
    struct StringTable
    {
        public UInt16 Length;
        public UInt16 ValueLength;
        public UInt16 Type;
        public char[] Key;  // 16字节，8个char，表示语言ID，比如："040904B0"
        public UInt16 Padding;
        public VerString[] Children;
        public StringTable(byte[] buff, int offset)
        {
            Length = (UInt16)(((0xffff & buff[offset + 1]) << 8) + buff[offset]);
            ValueLength = (UInt16)(((0xffff & buff[offset + 3]) << 8) + buff[offset + 2]);
            Type = (UInt16)(((0xffff & buff[offset + 5]) << 8) + buff[offset + 4]);
            Key = new char[8];
            for (int i = 0; i < 8; i++)
            {
                Key[i] = (char)(((0xffff & buff[offset + 7 + 2*i]) << 8) + buff[offset + 6 + 2*i]);
            }
            Padding = (UInt16)(((0xffff & buff[offset + 23]) << 8) + buff[offset + 22]);
            Link child = new Link();
            Link currentNode = child;
            int start = offset + 24;
            int currentPos = offset + 24;
            while (currentPos < offset + Length)
            {
                VerString vString = new VerString(buff, currentPos);
                currentNode.Value = vString;
                currentNode.Next = new Link();
                currentNode = currentNode.Next;
                child.NodeCount += 1;
                currentPos += vString.Length;
                if ((currentPos % 4) != 0)
                {
                    currentPos += 2;
                }               
            }
            Children = null;
            if (child.NodeCount > 0)
            {
                Children = new VerString[child.NodeCount];
                currentNode = child;
                for (int i = 0; i < child.NodeCount; i++)
                {
                    Children[i] = (VerString)currentNode.Value;
                    currentNode = currentNode.Next;
                }
            }
        }

        public void Export(string file)
        {
            string temp = "String Table\r\n";
            temp += "Length :" + Length.ToString() + "\r\n";
            temp += "ValueLength :" + ValueLength.ToString() + "\r\n";
            temp += "Type :" + Type.ToString() + "\r\n";
            temp += "Key :" + new String(Key) + "\r\n";
            temp += "Padding :" + Padding.ToString() + "\r\n";
            File.AppendAllText(file, temp);
            for (int i = 0; i < Children.Length; i++)
            {
                Children[i].Export(file);
            }
            File.AppendAllText(file, "------------------------------------------------------\r\n");
        }
    }
    struct VerString
    {
        public UInt16 Length;
        public UInt16 ValueLength;
        public UInt16 Type;
        public char[] Key;
        public UInt16 Padding;  // 因为 Key 是以 \0 结尾的字符串，因此其后必然有一个空字节，所以这里Padding必然存在并且等于0
        public char[] Value;

        public VerString(byte[] buff, int offset)
        {
            Length = (UInt16)(((0xffff & buff[offset + 1]) << 8) + buff[offset]);
            ValueLength = (UInt16)(((0xffff & buff[offset + 3]) << 8) + buff[offset + 2]);
            Type = (UInt16)(((0xffff & buff[offset + 5]) << 8) + buff[offset + 4]);
            int currentPos = offset + 6;
            int keyLen = 0;
            for (int i = offset + 6; i < offset + Length; i += 2)
            {
                UInt16 temp = (UInt16)(((0xffff & buff[i + 1]) << 8) + buff[i]);
                if (temp == 0)
                {
                    keyLen = (i - currentPos) / 2;
                    break;
                }
            }
            Key = new Char[keyLen];
            
            for (int i = 0; i < keyLen; i++)
            {
                Key[i] = (char)(((0xffff & buff[currentPos + 1 + 2 * i]) << 8) + buff[currentPos + 2 * i]);
            }
            currentPos += 2 * keyLen;
            Padding = (UInt16)(((0xffff & buff[currentPos + 1]) << 8) + buff[currentPos]); ;
            currentPos += 2;
            Value = new char[ValueLength];
            for (int i = 0; i < ValueLength; i++)
            {
                Value[i] = (char)(((0xffff & buff[currentPos + 1 + 2 * i]) << 8) + buff[currentPos + 2 * i]);
            }
        }

        public void Export(string file)
        {
            string temp = "-------------------------------------------\r\n";
            temp += "Length :" + Length.ToString() + "\r\n";
            temp += "ValueLength :" + ValueLength.ToString() + "\r\n";
            temp += "Type :" + Type.ToString() + "\r\n";
            temp += "Key :" + new String(Key) + "\r\n";
            temp += "Padding :" + Padding.ToString() + "\r\n";
            temp += "Value :" + new string(Value) + "\r\n";
            File.AppendAllText(file, temp);
        }
    }
    struct VarFileInfo
    {
        public UInt16 Length;
        public UInt16 ValueLength;
        public UInt16 Type;
        public char[] Key;
        public UInt32 Padding;  // 因为Key为11个字符，为了数据部分4字节对齐，此处Padding为4字节
        public Var[] Children;

        public VarFileInfo(byte[] buff, int offset)
        {
            Length = (UInt16)(((0xffff & buff[offset + 1]) << 8) + buff[offset]);
            ValueLength = (UInt16)(((0xffff & buff[offset + 3]) << 8) + buff[offset + 2]);
            Type = (UInt16)(((0xffff & buff[offset + 5]) << 8) + buff[offset + 4]);
            Key = new Char[11];
            for (int i = 0; i < 11; i++)
            {
                Key[i] = (char)(((0xffff & buff[offset + 7 + 2 * i]) << 8) + buff[offset + 6 + i * 2]);
            }
            Padding = (0xffffffff & buff[offset + 28]) +
                        ((0xffffffff & buff[offset + 29]) << 8) +
                        ((0xffffffff & buff[offset + 30]) << 16) +
                        ((0xffffffff & buff[offset + 31]) << 24);
            int currentPos = offset + 32;
            Link child = new Link();
            Link currentNode = child;
            
            while (currentPos < offset + Length)
            {
                Var var = new Var(buff, currentPos);
                currentNode.Value = var;
                currentNode.Next = new Link();
                currentNode = currentNode.Next;
                child.NodeCount ++;
                currentPos += var.Length;
                if ((currentPos % 4) != 0)
                {
                    currentPos += 2;
                }
            }
            Children = null;
            if (child.NodeCount > 0)
            {
                Children = new Var[child.NodeCount];
                currentNode = child;
                for (int i = 0; i < Children.Length; i++)
                {
                    Children[i] = (Var)currentNode.Value;
                    currentNode = currentNode.Next;
                }
            }
        }

        public void Export(string file)
        {
            string temp = "\r\nVar File Info\r\n";
            temp += "Length :" + Length.ToString() + "\r\n";
            temp += "ValueLength :" + ValueLength.ToString() + "\r\n";
            temp += "Type :" + Type.ToString() + "\r\n";
            temp += "Key :" + new String(Key) + "\r\n";
            temp += "Padding :" + Padding.ToString() + "\r\n";
            File.AppendAllText(file, temp);
            for (int i = 0; i < Children.Length; i++)
            {
                Children[i].Export(file);
            }
        }
    }
    struct Var
    {
        public UInt16 Length;
        public UInt16 ValueLength;
        public UInt16 Type;
        public char[] Key;
        public UInt32 Padding; 
        public UInt32[] Value;  // 记录语言的ID
        public Var(byte[] buff, int offset)
        {
            Length = (UInt16)(((0xffff & buff[offset + 1]) << 8) + buff[offset]);
            ValueLength = (UInt16)(((0xffff & buff[offset + 3]) << 8) + buff[offset + 2]);
            Type = (UInt16)(((0xffff & buff[offset + 5]) << 8) + buff[offset + 4]);
            Key = new Char[11];
            for (int i = 0; i < 11; i++)
            {
                Key[i] = (char)(((0xffff & buff[offset + 7 + 2 * i]) << 8) + buff[offset + 6 + i * 2]);
            }
            Padding = (0xffffffff & buff[offset + 28]) +
                        ((0xffffffff & buff[offset + 29]) << 8) +
                        ((0xffffffff & buff[offset + 30]) << 16) +
                        ((0xffffffff & buff[offset + 31]) << 24); 
            int currentPos = offset + 32;
            int valueCount = ValueLength / 4;
            Value = null;      
            if (valueCount > 0)
            {
                Value = new UInt32[valueCount];
                for (int i = 0; i < Value.Length; i++)
                {
                    Value[i] = (0xffffffff & buff[currentPos + 4 * i]) +
                               ((0xffffffff & buff[currentPos + 4 * i + 1]) << 8) +
                               ((0xffffffff & buff[currentPos + 4 * i + 2]) << 16) +
                               ((0xffffffff & buff[currentPos + 4 * i + 3]) << 24); 
                }
            }
        }

        public void Export(string file)
        {
            string temp = "Var File Info\r\n";
            temp += "Length :" + Length.ToString() + "\r\n";
            temp += "ValueLength :" + ValueLength.ToString() + "\r\n";
            temp += "Type :" + Type.ToString() + "\r\n";
            temp += "Key :" + new String(Key) + "\r\n";
            temp += "Padding :" + Padding.ToString() + "\r\n";
            temp += "Values: ";
            for (int i = 0; i < Value.Length; i++)
            {
                temp += Value[i].ToString("x8");
                if (i == (Value.Length - 1))
                {
                    temp += "\r\n";
                }
                else
                {
                    temp += ",";
                }
            }
            File.AppendAllText(file, temp);
        }
    }
    struct VersionFileInfo
    {
        public int InfoType;
        public Object Value;
    }
    #endregion

    #region Resource Type
    enum ResourceType
    {
        RT_CURSOR = 1,   // 光标
        RT_BITMAP = 2,   // 位图
        RT_ICON = 3,     // 图标
        RT_MENU = 4,     // 菜单
        RT_DIALOG = 5,   // 对话框
        RT_STRING = 6,   // 字符串
        RT_FONTDIR = 7,  // 字体目录
        RT_FONT = 8,           // 字体
        RT_ACCELERATOR = 9,    // 快捷键
        RT_RCDATA = 10,   // 自定义
        RT_MESSAGETABLE = 11,  // 消息表
        RT_GROUP_CURSOR = 12,   // 光标组
        RT_GROUP_ICON = 14,  // 图标组
        RT_VERSION = 16, // 版本信息 
        RT_DLGINCLUDE = 17,  // 包含资源文件，字符串格式
        RT_PLUGPLAY = 19,  // Plug and Play
        RT_VXD = 20,       // VXD
        RT_ANICURSOR = 21,  // 动画光标
        RT_ANIICON = 22,    // 动画图标
        RT_HTML = 23,     // HTML文档
        RT_MANIFEST = 24    // Manifest
    }
    #endregion

    #region Helper Class
    class Link
    {
        public int NodeType = 0;  // 使用这个成员是为了对Object对象进行准确拆箱
        public int NodeCount = 0;
        public Link Next = null;
        public Object Value = null;
    }
    #endregion
}
