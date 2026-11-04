// TOKDO DELIVERY - AI CHAT WIDGET
(function () {
    let conversationId = null;
    let isInitialized = false;

    function initAiChat() {
        const launcher = document.getElementById('tocdo-ai-chat-launcher');
        const chatBox = document.getElementById('tocdo-ai-chat-box');
        const closeBtn = document.getElementById('aiChatCloseBtn');
        const chatBody = document.getElementById('aiChatBody');
        const chatInput = document.getElementById('aiChatInput');
        const sendBtn = document.getElementById('aiChatSendBtn');
        const chipsContainer = document.getElementById('aiChatQuickChips');
        const toggleCalcBtn = document.getElementById('aiToggleCalcBtn');
        const miniCalc = document.getElementById('ai-mini-calculator');
        const calcSubmitBtn = document.getElementById('aiCalcSubmitBtn');

        if (!launcher || !chatBox) return;

        function toggleChat() {
            const isVisible = chatBox.classList.contains('active') || chatBox.style.display === 'flex';
            if (isVisible) {
                chatBox.style.display = 'none';
                chatBox.classList.remove('active');
            } else {
                chatBox.style.display = 'flex';
                chatBox.classList.add('active');
                if (chatInput) chatInput.focus();
                if (!isInitialized) {
                    loadChatData();
                } else {
                    scrollToBottom();
                }
            }
        }

        // Expose to window so onclick attribute can always trigger it reliably
        window.toggleAiChatWidget = toggleChat;

        launcher.addEventListener('click', (e) => {
            e.preventDefault();
            toggleChat();
        });

        if (closeBtn) {
            closeBtn.addEventListener('click', (e) => {
                e.preventDefault();
                chatBox.style.display = 'none';
                chatBox.classList.remove('active');
            });
        }

        // Toggle mini calculator tool
        if (toggleCalcBtn && miniCalc) {
            toggleCalcBtn.addEventListener('click', (e) => {
                e.preventDefault();
                if (miniCalc.style.display === 'block') {
                    miniCalc.style.display = 'none';
                } else {
                    miniCalc.style.display = 'block';
                }
            });
        }

        if (calcSubmitBtn) {
            calcSubmitBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                const w = parseFloat(document.getElementById('aiCalcWeight')?.value) || 2.0;
                const d = parseFloat(document.getElementById('aiCalcDist')?.value) || 5.0;
                const m = document.getElementById('aiCalcMethod')?.value || 'Thường';
                if (miniCalc) miniCalc.style.display = 'none';
                sendMessage(`Tính cước phí kiện hàng ${w}kg, cự ly ${d}km gói ${m}`);
            });
        }

        // Initialize chat and load history
        async function loadChatData() {
            isInitialized = true;
            if (chatBody) {
                chatBody.innerHTML = `
                    <div class="text-center py-4 text-muted small" id="aiInitLoading">
                        <div class="spinner-border spinner-border-sm text-primary me-1" role="status"></div>
                        Đang kết nối Trợ lý AI Tốc Độ...
                    </div>
                `;
            }

            try {
                const res = await fetch('/Chat/History');
                const data = await res.json();
                document.getElementById('aiInitLoading')?.remove();

                if (data.success) {
                    conversationId = data.conversationId;
                    if (data.messages && data.messages.length > 0) {
                        data.messages.forEach(m => {
                            appendMessage(m.content, m.isFromAi ? 'ai' : 'user', m.formattedTime);
                        });
                    } else {
                        const greeting = `Xin chào **${data.userFullName || 'bạn'}**! 👋 Em là **Trợ lý AI Tốc Độ**.\nEm có thể hỗ trợ bạn kiểm tra đơn hàng, tính cước phí dự kiến hoặc giải đáp các thắc mắc về giao nhận. Bạn cần em hỗ trợ gì ạ?`;
                        appendMessage(greeting, 'ai', 'Vừa xong');
                        renderQuickChips([
                            "📦 Đơn hàng của tôi",
                            "💰 Tính cước phí dự kiến",
                            "🏷️ Các loại hàng hóa",
                            "📍 Tìm bưu cục"
                        ]);
                    }
                } else {
                    appendMessage("Chào bạn! Em là Trợ lý AI của Tốc Độ Delivery. Em có thể giúp gì cho bạn?", 'ai', 'Vừa xong');
                }
                scrollToBottom();
            } catch (e) {
                console.error(e);
                document.getElementById('aiInitLoading')?.remove();
                appendMessage("Chào bạn! Em là Trợ lý AI Tốc Độ. Hãy nhắn điều bạn cần hỗ trợ nhé!", 'ai', 'Vừa xong');
            }
        }

        function appendMessage(text, type, timeStr) {
            if (!chatBody) return;
            const isAi = type === 'ai';
            const row = document.createElement('div');
            row.className = `ai-msg-row ${type}`;

            let formatted = (text || '')
                .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
                .replace(/\*(.*?)\*/g, '<em>$1</em>')
                .replace(/\n/g, '<br/>');

            row.innerHTML = `
                ${isAi ? '<div class="ai-chat-header-avatar" style="width: 28px; height: 28px; font-size: 13px; flex-shrink: 0;"><i class="bi bi-robot"></i></div>' : ''}
                <div class="ai-msg-bubble">
                    <div>${formatted}</div>
                    <div class="ai-msg-time">${timeStr || 'Vừa xong'}</div>
                </div>
            `;
            chatBody.appendChild(row);
            scrollToBottom();
        }

        function renderQuickChips(chips) {
            if (!chipsContainer) return;
            chipsContainer.innerHTML = '';
            if (!chips || chips.length === 0) return;

            chips.forEach(c => {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'ai-chip-btn';
                btn.textContent = c;
                btn.addEventListener('click', (e) => {
                    e.preventDefault();
                    sendMessage(c);
                });
                chipsContainer.appendChild(btn);
            });
        }

        function scrollToBottom() {
            if (chatBody) {
                chatBody.scrollTop = chatBody.scrollHeight;
            }
        }

        async function sendMessage(text) {
            if (!text || !text.trim() || !chatBody) return;
            const clean = text.trim();
            if (chatInput) chatInput.value = '';

            appendMessage(clean, 'user', new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }));

            // Typing indicator
            const typingEl = document.createElement('div');
            typingEl.className = 'ai-msg-row ai';
            typingEl.id = 'aiTypingBubble';
            typingEl.innerHTML = `
                <div class="ai-chat-header-avatar" style="width: 28px; height: 28px; font-size: 13px; flex-shrink: 0;"><i class="bi bi-robot"></i></div>
                <div class="ai-msg-bubble text-muted small" style="background: #ffffff;">
                    <span class="spinner-grow spinner-grow-sm text-warning me-1"></span> AI đang soạn câu trả lời...
                </div>
            `;
            chatBody.appendChild(typingEl);
            scrollToBottom();

            try {
                const res = await fetch('/Chat/SendMessage', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ message: clean, conversationId: conversationId })
                });
                const data = await res.json();
                typingEl.remove();

                if (data.success) {
                    if (data.conversationId) conversationId = data.conversationId;
                    appendMessage(data.reply, 'ai', new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }));

                    if (data.actionUrl && data.actionText) {
                        const actionRow = document.createElement('div');
                        actionRow.className = 'ai-msg-row ai ps-4';
                        actionRow.innerHTML = `
                            <a href="${data.actionUrl}" class="btn btn-sm btn-tocdo-orange rounded-pill px-3 shadow-sm fw-bold small">
                                ${data.actionText} <i class="bi bi-arrow-right ms-1"></i>
                            </a>
                        `;
                        chatBody.appendChild(actionRow);
                        scrollToBottom();
                    }

                    if (data.quickPrompts && data.quickPrompts.length > 0) {
                        renderQuickChips(data.quickPrompts);
                    }
                } else {
                    appendMessage("Rất tiếc, đã xảy ra lỗi kết nối. Vui lòng thử lại sau.", 'ai');
                }
            } catch (err) {
                console.error(err);
                typingEl.remove();
                appendMessage("Không thể gửi tin nhắn. Vui lòng kiểm tra lại kết nối mạng.", 'ai');
            }
        }

        if (sendBtn) {
            sendBtn.addEventListener('click', (e) => {
                e.preventDefault();
                sendMessage(chatInput?.value);
            });
        }

        if (chatInput) {
            chatInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    sendMessage(chatInput.value);
                }
            });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAiChat);
    } else {
        initAiChat();
    }
})();
