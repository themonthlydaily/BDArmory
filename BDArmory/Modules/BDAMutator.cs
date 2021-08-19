using BDArmory.Core;
using BDArmory.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BDArmory.Modules
{
	class BDAMutator : PartModule
	{
        float startTime;
        bool mutatorEnabled = false;
        public List<string> mutators;

        private MutatorInfo mutatorInfo;

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.force_activate();
            }
            base.OnStart(state);
        }

        public void EnableMutator(string name = "def")
        {
            if (mutatorEnabled) //replace current mutator with new one
            {
                DisableMutator();
            }
            if (name == "def") //mutator not specified, randomly choose from selected mutators
            {
                mutators = BDAcTools.ParseNames(BDArmorySettings.MUTATOR_LIST);
                int i = UnityEngine.Random.Range(0, mutators.Count);
                name = MutatorInfo.mutators[mutators[i]];
            }


            startTime = Time.time;
            mutatorEnabled = true; 
        }

        public void DisableMutator()
        {

        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (Time.time - startTime > BDArmorySettings.MUTATOR_DURATION)
                {
                    DisableMutator();
                }
            }
        }
    }
}

