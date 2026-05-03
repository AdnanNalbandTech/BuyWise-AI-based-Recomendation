import { CommonModule } from '@angular/common';
import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { CartService } from '../../core/cart.service';
import { ChatbotService } from '../../core/chatbot.service';
import { ChatbotResponse, Recommendation } from '../../core/models';
import { ProductService } from '../../core/product.service';

interface ChatMessage {
  role: 'user' | 'bot';
  text: string;
  response?: ChatbotResponse;
}

@Component({
  selector: 'app-chatbot',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './chatbot.component.html',
  styleUrl: './chatbot.component.css'
})
export class ChatbotComponent {
  private readonly chatbot = inject(ChatbotService);
  private readonly auth = inject(AuthService);
  private readonly cart = inject(CartService);
  private readonly products = inject(ProductService);
  private readonly router = inject(Router);

  @ViewChild('messageList') private messageList?: ElementRef<HTMLDivElement>;

  open = false;
  draft = '';
  loading = false;
  messages: ChatMessage[] = [
    {
      role: 'bot',
      text: "Hi, I'm your BuyWise shopping assistant. I can help you find products, recommend items, track orders, manage cart, and answer shopping questions."
    }
  ];

  quickReplies = [
    'Show me laptops under 50000',
    'Suggest shoes for running',
    'What should I buy with this phone?',
    'Where is my order?',
    'Show my cart total',
    'Return policy'
  ];

  toggle(): void {
    this.open = !this.open;
    this.scrollSoon();
  }

  send(text = this.draft): void {
    const message = text.trim();
    if (!message || this.loading) {
      return;
    }

    this.messages.push({ role: 'user', text: message });
    this.draft = '';
    this.loading = true;
    this.scrollSoon();

    this.chatbot.ask({
      message,
      userId: this.auth.currentUser?.id,
      currentProductId: this.currentProductId(),
      cartProductIds: this.cart.productIds()
    }).subscribe({
      next: (response) => {
        this.messages.push({ role: 'bot', text: response.reply, response });
        this.quickReplies = response.quickReplies?.length ? response.quickReplies : this.quickReplies;
        if (response.intent === 'CartAdd') {
          const productId = this.currentProductId();
          if (productId) {
            this.products.getProduct(productId).subscribe((product) => this.cart.addLocal(product));
          }
        }
        this.loading = false;
        this.scrollSoon();
      },
      error: () => {
        this.messages.push({
          role: 'bot',
          text: 'I could not reach the shopping assistant API. Please confirm the ASP.NET backend is running.'
        });
        this.loading = false;
        this.scrollSoon();
      }
    });
  }

  addProduct(product: Recommendation): void {
    this.products.getProduct(product.id).subscribe((fullProduct) => this.cart.add(fullProduct));
  }

  private currentProductId(): number | undefined {
    const match = this.router.url.match(/\/products\/(\d+)/);
    return match ? Number(match[1]) : undefined;
  }

  private scrollSoon(): void {
    setTimeout(() => {
      const element = this.messageList?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    });
  }
}
