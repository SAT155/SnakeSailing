# SnakeSquad mod art

In-game icons should look like Valheim's own: a small render of the real in-game object (the
boar, the longship, the ore), not drawn or stylised art.

- **Take the picture from the game at runtime.** Reuse a vanilla item or piece icon when one fits
  (`ItemDrop.m_itemData.m_shared.m_icons[0]`, `Piece.m_icon`), or render the real prefab with
  `IconRenderer.RenderPrefab` (SnakeSkills `DecorPlants.cs`), which strips a copy of the prefab to
  its meshes and renders it on a transparent background. Nothing from the game is shipped.
- **Keep an embedded PNG as the fallback** for when the prefab is missing.
- SnakeSkills skill icons (1.30): Sailing = Longship build icon, Ranching = render of the Boar,
  Mining = Copper ore icon, Landscaping = Hoe icon (`SkillIcons.cs`).
