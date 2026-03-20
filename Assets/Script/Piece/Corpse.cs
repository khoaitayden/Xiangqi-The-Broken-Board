using UnityEngine;

public class Corpse : MonoBehaviour
{
    public int turnsRemaining=7; 
    private BoardNode currentNode;

    public void Init(BoardNode node)
    {
        this.currentNode = node;
    }

    public void Decay()
    {
        turnsRemaining--;
        
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;

            if (turnsRemaining == 1)
            {
                // Halfway through decay: Become Ghostly (50% Alpha)
                c.a = 0.6f; 
            }
            // (If turnsRemaining is 2, it stays at 1.0f from Piece.cs)
            
            sr.color = c;
        }

        if (turnsRemaining <= 0)
        {
            if (currentNode != null)
            {
                currentNode.currentCorpse = null; // Clear the grid
            }
            Destroy(gameObject); // Destroy the visual clone
        }
    }
}