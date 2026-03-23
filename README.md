# S-Box-AMD-GPU-Guard

Fixes AMD Vulkan crash from envmap probes leaking descriptors across play/stop cycles // Drop this on any GameObject in your scene
`CRenderDeviceVulkan::UpdateDescriptorSetPool(): Vulkan failed to create descriptor pool.`
# How does this fix it?
Delays the map load on play start. It disables the MapInstance component for ~10 frames so the GPU has time to actually clean up resources from the last session before getting hammered with new cubemap renders.

Kills envmap probes the instant they spawn. As soon as the map finishes loading and the probes appear in the scene, it disables all of them before they get a chance to render. They keep their baked data so lighting still looks fine, they just stop doing expensive runtime cubemap re-renders that leak descriptors.

Aggressively cleans up on session end. When you hit stop, it disables all envmap probes, detaches render targets from cameras, and kills screen panels to help the driver reclaim as many descriptors as possible.

Also disables the ScreenPanel during the startup window to reduce descriptor pressure while the map is loading.
