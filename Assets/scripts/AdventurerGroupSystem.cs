using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Système de groupes d'aventuriers
/// Les aventuriers forment des groupes seulement si :
/// - Sont à l'extérieur du donjon
/// - Ont déjà défié et perdu
/// - Ont assez de sociabilité
/// </summary>
public class AdventurerGroupSystem : MonoBehaviour
{
    [Header("Group Settings")]
    [Tooltip("Distance pour rejoindre un groupe")]
    public float groupFormationDistance = 15f;
    
    [Tooltip("Temps d'attente max pour former un groupe (s)")]
    public float maxWaitTime = 20f;
    
    [Tooltip("Taille max d'un groupe")]
    public int maxGroupSize = 6;

    [Header("Sociability")]
    [Tooltip("Seuil minimum de sociabilité pour rejoindre un groupe")]
    [Range(0f, 1f)]
    public float minSociabilityThreshold = 0.3f;

    private List<AdventurerGroup> activeGroups = new List<AdventurerGroup>();

    public static AdventurerGroupSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        // Nettoyer groupes vides
        activeGroups.RemoveAll(g => g == null || g.GetMemberCount() == 0);
        
        // Mettre à jour les groupes
        foreach (var group in activeGroups)
        {
            group.UpdateGroup();
        }
    }

    /// <summary>
    /// Essaye de rejoindre ou créer un groupe (appelé après les soins).
    /// Retourne false si l'aventurier ne peut pas / ne veut pas attendre un groupe.
    /// </summary>
    public bool TryJoinOrCreateGroup(AdventurerAI adventurer)
    {
        if (!adventurer.CanJoinGroup() || adventurer.GetSociability() < minSociabilityThreshold)
            return false;

        AdventurerGroup nearbyGroup = FindNearbyGroup(adventurer.transform.position);
        if (nearbyGroup != null && nearbyGroup.CanAcceptMember())
        {
            nearbyGroup.AddMember(adventurer);
            DLog.Log($"[AdventurerGroup] {adventurer.DisplayName} rejoint groupe {nearbyGroup.groupID}");
        }
        else
        {
            AdventurerGroup newGroup = new AdventurerGroup(groupFormationDistance, maxWaitTime, maxGroupSize);
            newGroup.AddMember(adventurer);
            activeGroups.Add(newGroup);
            DLog.Log($"[AdventurerGroup] {adventurer.DisplayName} crée groupe {newGroup.groupID}");
        }
        return true;
    }

    /// <summary>
    /// Enregistrer aventurier qui spawn en groupe
    /// </summary>
    public void RegisterGroupSpawn(List<AdventurerAI> adventurers)
    {
        if (adventurers.Count == 0) return;

        // Créer groupe directement prêt
        AdventurerGroup newGroup = new AdventurerGroup(groupFormationDistance, 0f, maxGroupSize);
        
        foreach (var adv in adventurers)
        {
            newGroup.AddMember(adv);
        }
        
        // Groupe déjà prêt (pas d'attente)
        newGroup.ForceReady();
        activeGroups.Add(newGroup);
        
        DLog.Log($"[AdventurerGroup] Groupe de spawn {newGroup.groupID} créé avec {adventurers.Count} membres");
    }

    private AdventurerGroup FindNearbyGroup(Vector3 position)
    {
        foreach (var group in activeGroups)
        {
            if (group.IsNearby(position) && group.IsWaiting())
            {
                return group;
            }
        }
        return null;
    }
}

/// <summary>
/// Groupe d'aventuriers
/// </summary>
public class AdventurerGroup
{
    public string groupID;
    private List<AdventurerAI> members = new List<AdventurerAI>();
    private float formationDistance;
    private float maxWaitTime;
    private int maxSize;
    private float waitTimer;
    private bool isWaiting = true;
    private Vector3 rallyPoint;

    public AdventurerGroup(float formDist, float waitTime, int maxSz)
    {
        groupID = System.Guid.NewGuid().ToString().Substring(0, 8);
        formationDistance = formDist;
        maxWaitTime = waitTime;
        maxSize = maxSz;
        waitTimer = maxWaitTime;
    }

    public void AddMember(AdventurerAI adventurer)
    {
        if (!members.Contains(adventurer))
        {
            members.Add(adventurer);
            
            // Premier membre = définit rally point
            if (members.Count == 1)
            {
                rallyPoint = adventurer.transform.position;
            }
            
            adventurer.SetGroup(this);
            
            // Reset timer
            if (isWaiting && waitTimer < maxWaitTime)
            {
                waitTimer = maxWaitTime;
            }
        }
    }

    public void RemoveMember(AdventurerAI adventurer)
    {
        members.Remove(adventurer);
    }

    public void UpdateGroup()
    {
        // Nettoyer membres morts
        members.RemoveAll(m => m == null);
        
        if (members.Count == 0) return;

        // Timer d'attente
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            
            // Timer expiré ou groupe plein → ATTACK !
            if (waitTimer <= 0f || members.Count >= maxSize)
            {
                StartAttack();
            }
        }
    }

    private void StartAttack()
    {
        isWaiting = false;
        DLog.Log($"[AdventurerGroup] Groupe {groupID} ({members.Count} membres) ATTAQUE !");
        
        // Notifier tous les membres
        foreach (var member in members)
        {
            if (member != null)
            {
                member.OnGroupReady();
            }
        }
    }

    /// <summary>
    /// Force le groupe à être prêt (pour spawn groupés)
    /// </summary>
    public void ForceReady()
    {
        isWaiting = false;
        waitTimer = 0f;
        
        // Notifier membres
        foreach (var member in members)
        {
            if (member != null)
            {
                member.OnGroupReady();
            }
        }
    }

    public bool IsWaiting() => isWaiting;
    public bool CanAcceptMember() => members.Count < maxSize && isWaiting;
    public int GetMemberCount() => members.Count;
    public bool HasMember(AdventurerAI adventurer) => members.Contains(adventurer);
    
    public bool IsNearby(Vector3 position)
    {
        return Vector3.Distance(position, rallyPoint) <= formationDistance;
    }

    public Vector3 GetRallyPoint() => rallyPoint;
    public float GetWaitProgress() => 1f - (waitTimer / maxWaitTime);
}