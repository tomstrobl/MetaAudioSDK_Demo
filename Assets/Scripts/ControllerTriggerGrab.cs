using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace MetaAudioSDKDemo
{
    /// <summary>
    /// Lets controllers grab with the trigger (index finger) as well as the grip (middle finger).
    /// People new to Quest instinctively reach for the trigger, and the grip button is easy to miss.
    /// Widens the button set of every controller grab interactor in the scene while enabled.
    /// Don't use it in a scene where the trigger should do something else to a held object.
    /// </summary>
    public class ControllerTriggerGrab : MonoBehaviour
    {
        readonly List<ControllerSelector> widened = new();

        void OnEnable()
        {
            // The rig keeps its controller interactors inactive while hands are tracked, so include inactive ones.
            foreach (var interactor in FindObjectsByType<GrabInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Widen(interactor);
            foreach (var interactor in FindObjectsByType<DistanceGrabInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Widen(interactor);
        }

        void OnDisable()
        {
            foreach (var selector in widened)
                if (selector != null) selector.ControllerButtonUsage &= ~ControllerButtonUsage.TriggerButton;
            widened.Clear();
        }

        void Widen(Component interactor)
        {
            foreach (var selector in interactor.GetComponentsInChildren<ControllerSelector>(true))
            {
                var usage = selector.ControllerButtonUsage;
                if ((usage & ControllerButtonUsage.GripButton) == 0 || (usage & ControllerButtonUsage.TriggerButton) != 0) continue;
                // 'Any' keeps the grab held while either button is down; 'All' would demand both.
                if (selector.RequireButtonUsages != ControllerSelector.ControllerSelectorLogicOperator.Any) continue;
                selector.ControllerButtonUsage = usage | ControllerButtonUsage.TriggerButton;
                widened.Add(selector);
            }
        }
    }
}
