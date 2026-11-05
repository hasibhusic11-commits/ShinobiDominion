using System;

namespace ShinobiDominion.Core
{
    public enum VillageScreen
    {
        Home,
        BuildingPanel,
        BuildCatalog,
        PlacingBuilding,
        MovingBuilding,
        PaintingWalls,
        AttackFlow
    }

    /// <summary>
    /// Enforces the building-driven navigation rule: the build catalog is reachable only from a
    /// selected Builder Hall, and its palette and the grid overlay exist only inside that workflow.
    /// Engine-free, so these rules are testable without a scene.
    /// </summary>
    public sealed class VillageUiState
    {
        public VillageScreen Screen { get; private set; } = VillageScreen.Home;
        public int SelectedBuildingId { get; private set; }
        public BuildingFunction? SelectedFunction { get; private set; }

        public event Action Changed;

        public bool IsCatalogVisible => Screen == VillageScreen.BuildCatalog;
        public bool IsGridOverlayVisible =>
            Screen == VillageScreen.PlacingBuilding ||
            Screen == VillageScreen.MovingBuilding ||
            Screen == VillageScreen.PaintingWalls;

        /// <summary>The ATTACK button belongs to the idle home view and the building overlays over it.</summary>
        public bool IsAttackVisible => Screen == VillageScreen.Home || Screen == VillageScreen.BuildingPanel;

        public void SelectBuilding(BuildingInstance building)
        {
            if (Screen != VillageScreen.Home && Screen != VillageScreen.BuildingPanel) return;
            SelectedBuildingId = building.Id;
            SelectedFunction = building.Definition.function;
            Set(VillageScreen.BuildingPanel);
        }

        public void CloseToHome()
        {
            SelectedBuildingId = 0;
            SelectedFunction = null;
            Set(VillageScreen.Home);
        }

        /// <summary>Returns false unless a Builder Hall panel is the current screen.</summary>
        public bool TryOpenCatalog()
        {
            if (Screen != VillageScreen.BuildingPanel || SelectedFunction != BuildingFunction.BuilderHall)
                return false;
            Set(VillageScreen.BuildCatalog);
            return true;
        }

        public bool TryBeginPlacement(bool isWall)
        {
            if (Screen != VillageScreen.BuildCatalog) return false;
            Set(isWall ? VillageScreen.PaintingWalls : VillageScreen.PlacingBuilding);
            return true;
        }

        /// <summary>Leaving placement returns to the catalog, which still needs its Builder Hall context.</summary>
        public void EndPlacement()
        {
            if (Screen == VillageScreen.PlacingBuilding || Screen == VillageScreen.PaintingWalls)
                Set(VillageScreen.BuildCatalog);
        }

        public bool TryBeginMove()
        {
            if (Screen != VillageScreen.BuildingPanel) return false;
            Set(VillageScreen.MovingBuilding);
            return true;
        }

        public void EndMove()
        {
            if (Screen == VillageScreen.MovingBuilding) CloseToHome();
        }

        public bool TryOpenAttack()
        {
            if (!IsAttackVisible) return false;
            SelectedBuildingId = 0;
            SelectedFunction = null;
            Set(VillageScreen.AttackFlow);
            return true;
        }

        private void Set(VillageScreen screen)
        {
            Screen = screen;
            Changed?.Invoke();
        }
    }
}
