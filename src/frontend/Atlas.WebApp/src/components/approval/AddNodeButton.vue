<template>
  <div class="add-node-btn-box">
    <div class="add-node-btn">
      <a-popover placement="right" trigger="click" v-model:open="visible">
        <template #content>
          <div class="add-node-popover-body">
            <div class="add-node-popover-item" @click="handleSelect('approve')">
              <div class="item-wrapper">
                <UserOutlined class="icon" style="color: #ff943e" />
              </div>
              <p>审批人</p>
            </div>
            <div class="add-node-popover-item" @click="handleSelect('copy')">
              <div class="item-wrapper">
                <SendOutlined class="icon" style="color: #3296fa" />
              </div>
              <p>抄送人</p>
            </div>
            <div class="add-node-popover-item" @click="handleSelect('condition')">
              <div class="item-wrapper">
                <BranchesOutlined class="icon" style="color: #15bc83" />
              </div>
              <p>条件分支</p>
            </div>
          </div>
        </template>
        <button class="btn">
          <PlusOutlined />
        </button>
      </a-popover>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { PlusOutlined, UserOutlined, SendOutlined, BranchesOutlined } from '@ant-design/icons-vue';

const emit = defineEmits<{
  select: [nodeType: string];
}>();

const visible = ref(false);

const handleSelect = (type: string) => {
  emit('select', type);
  visible.value = false;
};
</script>

<style scoped>
.add-node-btn-box {
  width: 240px;
  display: flex;
  justify-content: center;
  padding: 20px 0;
  position: relative;
}

.add-node-btn-box::before {
  content: "";
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: -1;
  margin: auto;
  width: 2px;
  height: 100%;
  background-color: #cacaca;
}

.btn {
  outline: none;
  box-shadow: 0 2px 4px 0 rgba(0, 0, 0, 0.1);
  width: 30px;
  height: 30px;
  background: #3296fa;
  border-radius: 50%;
  position: relative;
  border: none;
  line-height: 30px;
  transition: all 0.3s;
  cursor: pointer;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
}

.btn:hover {
  transform: scale(1.2);
  box-shadow: 0 4px 8px 0 rgba(0, 0, 0, 0.2);
}

.add-node-popover-body {
  display: flex;
  gap: 10px;
}

.add-node-popover-item {
  cursor: pointer;
  text-align: center;
  width: 80px;
}

.add-node-popover-item .item-wrapper {
  width: 50px;
  height: 50px;
  margin: 0 auto;
  border: 1px solid #e2e2e2;
  border-radius: 50%;
  display: flex;
  justify-content: center;
  align-items: center;
  transition: all 0.3s;
}

.add-node-popover-item:hover .item-wrapper {
  background: #f0f0f0;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.add-node-popover-item .icon {
  font-size: 24px;
}

.add-node-popover-item p {
  margin-top: 5px;
  font-size: 12px;
  color: #333;
}
</style>
