using System;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.Entities
{
    /// <summary>
    /// 基于表面的三维模型（实体）
    /// 代表一个完整的基础三维模型，包含顶面、底面、墙体和底板
    /// </summary>
    public class SurfaceBasedModel
    {
        public Surface3D TopSurface { get; }
        public Surface3D BottomSurface { get; }
        public WallSolid3D WallSolid { get; }
        public SlabSolid3D SlabSolid { get; }
        public bool ContactsWithSoil { get; }
        
        private SurfaceBasedModel(
            Surface3D topSurface,
            Surface3D bottomSurface,
            WallSolid3D wallSolid,
            SlabSolid3D slabSolid,
            bool contactsWithSoil)
        {
            TopSurface = topSurface ?? throw new ArgumentNullException(nameof(topSurface));
            BottomSurface = bottomSurface ?? throw new ArgumentNullException(nameof(bottomSurface));
            WallSolid = wallSolid ?? throw new ArgumentNullException(nameof(wallSolid));
            SlabSolid = slabSolid ?? throw new ArgumentNullException(nameof(slabSolid));
            ContactsWithSoil = contactsWithSoil;
        }
        
        public static SurfaceBasedModel Create(
            Surface3D topSurface,
            Surface3D bottomSurface,
            WallSolid3D wallSolid,
            SlabSolid3D slabSolid,
            bool contactsWithSoil)
        {
            return new SurfaceBasedModel(
                topSurface,
                bottomSurface,
                wallSolid,
                slabSolid,
                contactsWithSoil);
        }
        
        public override string ToString()
        {
            return $"三维模型:\n" +
                   $"  - {TopSurface}\n" +
                   $"  - {BottomSurface}\n" +
                   $"  - {WallSolid}\n" +
                   $"  - {SlabSolid}\n" +
                   $"  - 土壤接触: {ContactsWithSoil}";
        }
    }
}

