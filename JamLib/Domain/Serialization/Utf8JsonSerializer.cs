﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JamLib.Domain.Serialization
{
    public class Utf8JsonSerializer: ISerializer
    {
        public StructType GetStructFromBytes<StructType>(byte[] rawBytes)
        {
            string structText = Encoding.UTF8.GetString(rawBytes);
            return JsonConvert.DeserializeObject<StructType>(structText);
        }

        public byte[] GetBytesFromStruct<StructType>(StructType structType)
        {
            JsonSerializerSettings ignoreLoopback = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            string structText = JsonConvert.SerializeObject(structType, Formatting.None, ignoreLoopback);
            return Encoding.UTF8.GetBytes(structText);
        }
    }
}
