using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace Monocle
{
    public class RuntimeSaveData
    {
        private Dictionary<string, object> dataRoom;

        public RuntimeSaveData()
        {
            dataRoom = new Dictionary<string, object>();
        }

        public void AddKey(string _key)
        {
            dataRoom.Add(_key, null);
        }

        public void SaveData(string _name, object _obj)
        {
            dataRoom [_name] = _obj;
        }
        public object GetData(string _name)
        {
            return dataRoom[_name];
        }
        public float GetFloat(string _name)
        {
            object retVal = GetData(_name);

            if (retVal is double)
                return (float)(double)retVal;
            else return (float)retVal;
        }
        public Vector2 GetVector2(string _name) {
            return (Vector2)dataRoom[_name];
        }
        public Vector3 GetVector3(string _name) {
            return (Vector3)dataRoom[_name];
        }
        public int GetInt(string _name)
        {
            object retVal = GetData(_name);

            if (retVal is long)
                return (int)(long)retVal;
            else return (int)retVal;
        }
        public string GetString(string _name)
        {
            object retVal = GetData(_name);

            return retVal as string;
        }
        public bool GetBool(string _name)
        {
            object obj = GetData(_name);

            return (bool)obj;
        }
    }
}