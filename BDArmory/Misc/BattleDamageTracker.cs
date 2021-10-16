using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Misc;
using BDArmory.Modules;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Misc
{
	public class BattleDamageTracker : MonoBehaviour
	{
		public float oldDamagePercent = 1;
		public double origIntakeArea = 1;

        public Part Part
        {
            get
            {
                return Part;
            }
            set
            {
                Part = value;
            }
        }

        void Awake()
        {
            if (!Part)
            {
                Part = GetComponent<Part>();
            }
            if (!Part)
            {
                //Debug.Log ("[BDArmory]: BDTracker attached to non-part, removing");
                Destroy(this);
                return;
            }
            //destroy this there's already one attached
            foreach (var prevTracker in Part.gameObject.GetComponents<BattleDamageTracker>())
            {
                if (prevTracker != this)
                {
                    Destroy(this);
                    return;
                }
            }           

            Part.OnJustAboutToBeDestroyed += AboutToBeDestroyed;
        }
        void OnDestroy()
        {
            Part.OnJustAboutToBeDestroyed -= AboutToBeDestroyed;
        }
        void AboutToBeDestroyed()
        {
            Destroy(this);
        }

    }
}
