using System;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Bullets
{
    public class BulletInfo
    {
        public string name { get; private set; }
        public float caliber { get; private set; }
        public float bulletMass { get; private set; }
        public float bulletVelocity { get; private set; }
        public bool explosive { get; private set; }
        public float tntMass { get; private set; }
        public string fuzeType { get; private set; }
        public int subProjectileCount { get; private set; }
        public float apBulletMod { get; private set; }
        public string bulletDragTypeName { get; private set; }
        public string projectileColor { get; private set; }
        public string startColor { get; private set; }
        public bool fadeColor { get; private set; }

        public static BulletInfos bullets;

        public BulletInfo(string name, float caliber, float bulletVelocity, float bulletMass,
                          bool explosive, float tntMass, string fuzeType, float apBulletDmg,
                          int subProjectileCount, string bulletDragTypeName, string projectileColor, string startColor, bool fadeColor)
        {
            this.name = name;
            this.caliber = caliber;
            this.bulletVelocity = bulletVelocity;
            this.bulletMass = bulletMass;
            this.explosive = explosive;
            this.tntMass = tntMass;
            this.fuzeType = fuzeType;
            this.apBulletMod = apBulletDmg;
            this.subProjectileCount = subProjectileCount;
            this.bulletDragTypeName = bulletDragTypeName;
            this.projectileColor = projectileColor;
            this.startColor = startColor;
            this.fadeColor = fadeColor;
        }

        public static void Load()
        {
            bullets = new BulletInfos();
            UrlDir.UrlConfig[] nodes = GameDatabase.Instance.GetConfigs("BULLET");
            for (int i = 0; i < nodes.Length; i++)
            {
                string name_ = "";
                try
                {
                    ConfigNode node = nodes[i].config;
                    name_ = (string)ParseField(node, "name", typeof(string));
                    bullets.Add(
                        new BulletInfo(
                            name_,
                            (float)ParseField(node, "caliber", typeof(float)),
                            (float)ParseField(node, "bulletVelocity", typeof(float)),
                            (float)ParseField(node, "bulletMass", typeof(float)),
                            (bool)ParseField(node, "explosive", typeof(bool)),
                            (float)ParseField(node, "tntMass", typeof(float)),
                            (string)ParseField(node, "fuzeType", typeof(string)),
                            (float)ParseField(node, "apBulletMod", typeof(float)),
                            (int)ParseField(node, "subProjectileCount", typeof(int)),
                            (string)ParseField(node, "bulletDragTypeName", typeof(string)),
                            (string)ParseField(node, "projectileColor", typeof(string)),
                            (string)ParseField(node, "startColor", typeof(string)),
                            (bool)ParseField(node, "fadeColor", typeof(bool))
                        )
                    );
                }
                catch (Exception e)
                {
                    Debug.LogError("[BDArmory]: Error Loading Bullet Config '" + name_ + "' | " + e.ToString());
                }
            }
        }

        private static object ParseField(ConfigNode node, string field, Type type)
        {
            if (!node.HasValue(field))
                throw new ArgumentNullException(field, "Field '" + field + "' is missing.");
            var value = node.GetValue(field);
            try
            {
                if (type == typeof(string))
                { return value; }
                else if (type == typeof(bool))
                { return bool.Parse(value); }
                else if (type == typeof(int))
                { return int.Parse(value); }
                else if (type == typeof(float))
                { return float.Parse(value); }
                else
                { throw new ArgumentException("Invalid type specified."); }
            }
            catch (Exception e)
            { throw new ArgumentException("Field '" + field + "': '" + value + "' could not be parsed as '" + type.ToString() + "' | " + e.ToString(), field); }
        }
    }

    public class BulletInfos : List<BulletInfo>
    {
        public BulletInfo this[string name]
        {
            get { return Find((value) => { return value.name == name; }); }
        }
    }
}
