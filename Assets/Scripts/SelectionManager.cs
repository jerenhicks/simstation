using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// Handles agent selection via mouse click.
/// Left-click an agent to select it (shows a green ring).
/// Left-click empty space to deselect.
/// Only one agent can be selected at a time.
///
/// Added to the scene automatically by SceneBuilder.
/// </summary>
public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    public AIAgent SelectedAgent { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            HandleClick();
    }

    private void HandleClick()
    {
        // Don't process world clicks when the pointer is over a UI element —
        // this prevents UI button clicks (e.g. Delete Agent) from accidentally
        // deselecting the current agent before the button's onClick fires.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Camera.main == null) return;

        var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out var hit))
        {
            // Check the hit object and all its parents for an AIAgent component
            var agent = hit.collider.GetComponentInParent<AIAgent>();

            if (agent != null)
            {
                SelectAgent(agent);
                return;
            }
        }

        // Clicked on empty space — deselect
        DeselectCurrent();
    }

    private void SelectAgent(AIAgent agent)
    {
        if (SelectedAgent == agent) return; // clicking the already-selected agent does nothing

        DeselectCurrent();
        SelectedAgent = agent;
        SelectedAgent.SetSelected(true);
    }

    public void DeselectCurrent()
    {
        if (SelectedAgent == null) return;
        SelectedAgent.SetSelected(false);
        SelectedAgent = null;
    }
}
