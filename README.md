# Path-Directed WFC Algorithm

- The Wave Function Collapse (WFC) algorithm, a procedural generation model, holds all possible map tiles for a single cell in a superposition state, which is then collapsed based on the state of adjacent cells to return the final tile (A). However, it was found that the combination of WFC's microscopic state rules inevitably resulted in noise on a map-wide scale. Furthermore, there was no guarantee that a path to the destination would be connected (B).
  
- For controlled randomness, a path generation algorithm - simulating lightning traversing electric potential valleys - provided a macroscopic guideline, while the WFC algorithm was responsible for microscopic representation (C). By first designating the cells the lightning passes through as path attributes, and then sequentially collapsing the surrounding cells according to the WFC principle, it became possible to create a natural-looking map with a guaranteed path (C).
  
<br>
<div style="display: flex; gap: 10px;">
  <img width="1920" height="1200" alt="image" src="https://github.com/user-attachments/assets/1a3601f0-4fb3-41f0-84ac-997e67c39ff5" />
</div>
<br>
