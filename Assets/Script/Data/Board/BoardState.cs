using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BoardState
{
    public struct PieceData
    {
        public Piece pieceReference; 
        public int x;
        public int y;
        public int hp;
        public int cooldown;
    }

    public List<PieceData> savedPieces = new List<PieceData>();
    public List<Corpse> savedCorpses = new List<Corpse>(); // NEW: Track Corpses
    public int playerAmmo;
    public int playerX;
    public int playerY;

    // Updated constructor to accept the corpse list
    public BoardState(PlayerGeneral player, List<Piece> enemies, List<Corpse> corpses)
    {
        playerAmmo = player.LoadedAmmo; 
        playerX = player.X;
        playerY = player.Y;

        foreach (Piece p in enemies)
        {
            if (p != null)
            {
                PieceData data = new PieceData
                {
                    pieceReference = p,
                    x = p.X,
                    y = p.Y,
                    hp = p.CurrentHp,
                    cooldown = p.CurrentCooldown
                };
                savedPieces.Add(data);
            }
        }

        // Save a snapshot of exactly which corpses exist right now
        savedCorpses.AddRange(corpses);
    }
}