export interface Category {
  id: number;
  name: string;
  slug: string;
  description: string;
  imageUrl: string;
}

export interface Product {
  id: number;
  name: string;
  description: string;
  price: number;
  imageUrl: string;
  stock: number;
  categoryId: number;
  categoryName: string;
  rating: number;
  reviewCount: number;
  brand: string;
  tags: string;
  featured: boolean;
  createdAt: string;
}

export interface ProductUpsert {
  name: string;
  description: string;
  price: number;
  imageUrl: string;
  stock: number;
  categoryId: number;
  rating: number;
  reviewCount: number;
  brand: string;
  tags: string;
  featured: boolean;
}

export interface ProductFilters {
  search?: string;
  categoryId?: number;
  minPrice?: number;
  maxPrice?: number;
  brand?: string;
  minRating?: number;
  tags?: string;
}

export interface Recommendation {
  id: number;
  name: string;
  price: number;
  imageUrl: string;
  categoryName: string;
  rating: number;
  reason: string;
  score: number;
}

export interface PublicUser {
  id: number;
  fullName: string;
  email: string;
  role: string;
}

export interface AuthResponse {
  token: string;
  user: PublicUser;
}

export interface CartItem extends Product {
  quantity: number;
}

export interface ServerCartItem {
  productId: number;
  productName: string;
  price: number;
  imageUrl: string;
  categoryName: string;
  brand: string;
  quantity: number;
  lineTotal: number;
}

export interface CartSummary {
  userId: number;
  items: ServerCartItem[];
  total: number;
}

export interface UserActivityRequest {
  userId: number;
  productId: number;
  activityType: 'View' | 'CartAdd' | 'Wishlist' | 'Purchase';
  quantity: number;
}

export interface OrderItemRequest {
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
}

export interface OrderRequest {
  userId: number;
  fullName: string;
  email: string;
  shippingAddress: string;
  items: OrderItemRequest[];
}

export interface OrderResponse {
  id: number;
  total: number;
  createdAt: string;
  status: string;
  trackingNumber?: string;
  estimatedDelivery?: string;
}

export interface OrderSummary extends OrderResponse {
  items: OrderItemRequest[];
}

export interface ChatbotRequest {
  message: string;
  userId?: number;
  currentProductId?: number;
  cartProductIds?: number[];
}

export interface ChatbotResponse {
  reply: string;
  intent: string;
  products: Recommendation[];
  cart?: CartSummary;
  order?: OrderSummary;
  quickReplies: string[];
}
