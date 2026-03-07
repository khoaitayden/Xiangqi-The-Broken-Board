using UnityEngine;

public class Corpse : MonoBehaviour
{
    public int turnsRemaining = 2; 
    private BoardNode currentNode;

    // We call this when the Piece dies
    public void Init(BoardNode node)
    {
        this.currentNode = node;
    }

    public void Decay()
    {
        turnsRemaining--;
        
        // Visual Feedback: Fade out as it rots
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a = (turnsRemaining > 0) ? 0.5f : 0f; // 50% opacity, then 0
            sr.color = c;
        }

        if (turnsRemaining <= 0)
        {
            if (currentNode != null)
            {
                currentNode.currentCorpse = null; // Clear the grid
            }
            Destroy(gameObject); // Finally destroy the GameObject
        }
    }
}