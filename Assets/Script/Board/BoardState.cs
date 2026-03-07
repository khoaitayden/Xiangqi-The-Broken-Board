using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BoardState
{
    // We save the position, health, and cooldown of every piece
    public struct PieceData
    {
        public Piece pieceReference; // Which object is this?
        public int x;
        public int y;
        public int hp;
        public int cooldown;
        public bool isDead; // Did it die this turn?
    }

    public List<PieceData> savedPieces = new List<PieceData>();
    public int playerAmmo;
    public int playerX;
    public int playerY;

    // Capture the current state of the board
    public BoardState(PlayerGeneral player, List<Piece> enemies)
    {
        // Save Player
        playerAmmo = player.LoadedAmmo;
        playerX = player.X;
        playerY = player.Y;

        // Save Enemies
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
                    cooldown = p.CurrentCooldown,
                    isDead = false
                };
                savedPieces.Add(data);
            }
        }
    }
}