using CromulentBisgetti.ContainerPacking.Algorithms;
using CromulentBisgetti.ContainerPacking.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CromulentBisgetti.ContainerPacking
{
    /// <summary>
    /// The container packing service.
    /// </summary>
    public static class PackingService
	{
		/// <summary>
		/// Attempts to pack the specified containers with the specified items using the specified algorithms.
		/// </summary>
		/// <param name="containers">The list of containers to pack.</param>
		/// <param name="itemsToPack">The items to pack.</param>
		/// <param name="algorithmTypeIDs">The list of algorithm type IDs to use for packing.</param>
		/// <returns>A container packing result with lists of the packed and unpacked items.</returns>
		public static List<ContainerPackingResult> Pack(List<Container> containers, List<Item> itemsToPack, List<int> algorithmTypeIDs)
		{
			Object sync = new Object { };
			List<ContainerPackingResult> result = new List<ContainerPackingResult>();

			Parallel.ForEach(containers, container =>
			{
				ContainerPackingResult containerPackingResult = new ContainerPackingResult();
				containerPackingResult.ContainerID = container.ID;

				Parallel.ForEach(algorithmTypeIDs, algorithmTypeID =>
				{
					IPackingAlgorithm algorithm = GetPackingAlgorithmFromTypeID(algorithmTypeID);

					// Until I rewrite the algorithm with no side effects, we need to clone the item list
					// so the parallel updates don't interfere with each other.
					List<Item> items = new List<Item>();

					itemsToPack.ForEach(item =>
					{
						items.Add(new Item(item.ID, item.Dim1, item.Dim2, item.Dim3, item.Quantity));
					});

					Stopwatch stopwatch = new Stopwatch();
					stopwatch.Start();
					AlgorithmPackingResult algorithmResult = algorithm.Run(container, items);
					stopwatch.Stop();

					algorithmResult.PackTimeInMilliseconds = stopwatch.ElapsedMilliseconds;

					decimal containerVolume = container.Length * container.Width * container.Height;
					decimal itemVolumePacked = algorithmResult.PackedItems.Sum(i => i.Volume);
					decimal itemVolumeUnpacked = algorithmResult.UnpackedItems.Sum(i => i.Volume);

					algorithmResult.PercentContainerVolumePacked = Math.Round(itemVolumePacked / containerVolume * 100, 2);
					algorithmResult.PercentItemVolumePacked = Math.Round(itemVolumePacked / (itemVolumePacked + itemVolumeUnpacked) * 100, 2);

					lock (sync)
					{
						containerPackingResult.AlgorithmPackingResults.Add(algorithmResult);
					}
				});

				containerPackingResult.AlgorithmPackingResults = containerPackingResult.AlgorithmPackingResults.OrderBy(r => r.AlgorithmName).ToList();

				lock (sync)
				{
					result.Add(containerPackingResult);
				}
			});
			
			return result;
		}

		public static List<ContainerPackingResult> PackTotal(List<Container> containers,
			List<Item> itemsToPack,
			List<int> algorithmTypeIDs)
		{
			var itemsCount = 0;
			var min = 0;
			var max = 2;
			var power = 1;
			List<ContainerPackingResult> result = null;
			while (true)
			{
				List<Item> items = itemsToPack.Select(x => new Item(x.ID, x.Dim1, x.Dim2, x.Dim3, max)).ToList();
				var maxResult = PackingService.Pack(containers, items, algorithmTypeIDs);

				if (!maxResult[0].AlgorithmPackingResults[0].IsCompletePack)
				{
					break;
				}

				max = (int)Math.Pow(2, power);
				power++;
			}
			
			while (min <= max)
			{
				var mid = (min + max) / 2;
				List<Item> items = itemsToPack.Select(x => new Item(x.ID, x.Dim1, x.Dim2, x.Dim3, mid)).ToList();

				result = PackingService.Pack(containers, items, algorithmTypeIDs);
				if (result[0].AlgorithmPackingResults[0].IsCompletePack)
				{
					var nextItem = itemsToPack.Select(x => new Item(x.ID, x.Dim1, x.Dim2, x.Dim3, mid + 1)).ToList();
					var rightResult = PackingService.Pack(containers, nextItem, algorithmTypeIDs);
					if (!rightResult[0].AlgorithmPackingResults[0].IsCompletePack)
					{
						itemsCount = mid;
						break;
					}
				}
				if (!result[0].AlgorithmPackingResults[0].IsCompletePack)
				{
					max = mid - 1;
				}
				else
				{
					min = mid + 1;
				}
			}

			result[0].AlgorithmPackingResults[0].TotalItemsInContainer = itemsCount;
			return result;
		}
		
		/// <summary>
		/// Gets the packing algorithm from the specified algorithm type ID.
		/// </summary>
		/// <param name="algorithmTypeID">The algorithm type ID.</param>
		/// <returns>An instance of a packing algorithm implementing AlgorithmBase.</returns>
		/// <exception cref="System.Exception">Invalid algorithm type.</exception>
		public static IPackingAlgorithm GetPackingAlgorithmFromTypeID(int algorithmTypeID)
		{
			switch (algorithmTypeID)
			{
				case (int)AlgorithmType.EB_AFIT:
					return new EB_AFIT();

				default:
					throw new Exception("Invalid algorithm type.");
			}
		}
	}
}
