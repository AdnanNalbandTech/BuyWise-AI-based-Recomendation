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
}
