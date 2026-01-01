using System.Collections.Generic;
using UnityEngine;

public class ClueListPanelUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ClueListItemUI itemPrefab;

    [Header("Popup")]
    [SerializeField] private Transform popupParent;
    [SerializeField] private ClueDetailPopupUI popupPrefab;

    private readonly Dictionary<string, ClueListItemUI> _itemsById = new Dictionary<string, ClueListItemUI>();

    private ClueManager _manager;
    private bool _subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();

        if (contentRoot == null)
        {
            Debug.LogError("ClueListPanelUI: contentRoot is not assigned.");
        }

        if (itemPrefab == null)
        {
            Debug.LogError("ClueListPanelUI: itemPrefab is not assigned.");
        }

        if (popupPrefab == null)
        {
            Debug.LogWarning("ClueListPanelUI: popupPrefab is not assigned. Clicking clue items will do nothing.");
        }
    }

    private void OnDisable()
    {
        if (_subscribed && _manager != null)
        {
            _manager.OnClueRevealed -= HandleClueRevealed;
        }

        _subscribed = false;
        _manager = null;
    }

    private void TrySubscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _manager = ClueManager.instance;
        if (_manager == null)
        {
            _manager = FindObjectOfType<ClueManager>();
        }

        if (_manager == null)
        {
            Debug.LogWarning("ClueListPanelUI: ClueManager not found yet (will not receive reveal events).");
            return;
        }

        _manager.OnClueRevealed += HandleClueRevealed;
        _subscribed = true;
        Debug.Log("ClueListPanelUI: Subscribed to ClueManager.OnClueRevealed.");
    }

    private void HandleClueRevealed(ClueData clue)
    {
        if (clue == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(clue.id))
        {
            return;
        }

        if (_itemsById.ContainsKey(clue.id))
        {
            return;
        }

        if (contentRoot == null || itemPrefab == null)
        {
            Debug.LogWarning("ClueListPanelUI: Cannot spawn item because contentRoot or itemPrefab is null.");
            return;
        }

        var item = Instantiate(itemPrefab, contentRoot);
        item.Bind(clue);
        item.OnClicked += HandleItemClicked;
        _itemsById.Add(clue.id, item);
        Debug.Log($"ClueListPanelUI: Spawned clue item for {clue.id} under {contentRoot.name}. Total items: {_itemsById.Count}");
    }

    private void HandleItemClicked(ClueData clue)
    {
        if (popupPrefab == null)
        {
            return;
        }

        var parent = popupParent != null ? popupParent : transform;
        var popup = Instantiate(popupPrefab, parent);
        popup.Show(clue);
    }
}
