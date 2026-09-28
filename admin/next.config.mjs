/** @type {import('next').NextConfig} */
const nextConfig = {
  output: "export",
  experimental: {
    cpus: 1,
  },
  typescript: {
    ignoreBuildErrors: true,
  },

  devIndicators: {
    buildActivity: false,
    buildActivityPosition: "bottom-right",
  },

  images: {
    unoptimized: true,
  },
};

export default nextConfig;
