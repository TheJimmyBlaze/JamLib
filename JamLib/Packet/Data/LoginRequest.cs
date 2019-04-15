﻿using JamLib.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JamLib.Packet.Data
{
    public struct LoginRequest
    {
        public const int DATA_TYPE = 1;
        
        public string Username { get; set; }
        public string Password { get; set; }

        public LoginRequest(byte[] rawBytes)
        {
            this = StructMarshal.GetStructFromBytes<LoginRequest>(rawBytes);
        }

        public byte[] GetBytes()
        {
            return StructMarshal.GetBytesFromStruct(this);
        }
    }
}
